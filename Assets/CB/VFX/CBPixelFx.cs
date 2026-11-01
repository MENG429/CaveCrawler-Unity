using System;
using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Patch-gg/pixelfx sprite-sheet integration (MIT). Low-risk spike: parses the Aseprite-style
    /// JSON that `tools/render.mjs --sheet` emits and turns each baked effect into a clip of
    /// FullRect sprites. Loading is lazy and fully degrades: a missing PNG/JSON simply yields no
    /// clip, and callers (CBFx.SpawnEffect) fall back to their placeholder art.
    ///
    /// Runtime asset layout (must sit under a Resources folder so Resources.Load can find it,
    /// mirroring CBArt's convention - no asmdef / Addressables needed):
    ///   Assets/CB/External/pixelfx/Resources/&lt;category&gt;_&lt;name&gt;.png   (transparent frame grid)
    ///   Assets/CB/External/pixelfx/Resources/&lt;category&gt;_&lt;name&gt;.json  (Aseprite-style sheet JSON)
    ///
    /// Import settings (point filter, no compression, no mipmaps) are enforced by
    /// CBPixelFxImporter (Editor). The final palette and pixel scale are owned by the art-director;
    /// this spike uses placeholder values to prove the bake -&gt; import -&gt; play -&gt; wire pipeline.
    /// </summary>
    public static class CBPixelFx
    {
        /// <summary>
        /// World pixels-per-unit used when slicing baked frames. Must match the game's art PPU
        /// (CBArtImporter uses 16 -> 1 tile = 16 px) so effects sit at the same scale as sprites.
        /// The art-director owns any per-effect size tuning on top of this.
        /// </summary>
        public static float PixelsPerUnit = 16f;

        static readonly Dictionary<string, CBPixelFxClip> s_clips = new Dictionary<string, CBPixelFxClip>();

        /// <summary>
        /// Lazily load (and cache) the clip for an effect name such as "explosions_barrel-explosion".
        /// Returns false (and leaves clip == null) when the asset is missing or malformed, so callers
        /// can fall back. Safe to call every frame.
        /// </summary>
        public static bool TryGetClip(string name, out CBPixelFxClip clip)
        {
            clip = null;
            if (string.IsNullOrEmpty(name)) return false;
            if (s_clips.TryGetValue(name, out clip)) return clip != null;

            clip = Load(name);
            s_clips[name] = clip;   // cache null too, so we only warn once per name
            return clip != null;
        }

        /// <summary>Drop cached clips (e.g. on a hot-reload / level reset).</summary>
        public static void ClearCache()
        {
            s_clips.Clear();
        }

        static CBPixelFxClip Load(string name)
        {
            var tex = Resources.Load<Texture2D>(name);
            var json = Resources.Load<TextAsset>(name);
            if (tex == null || json == null || string.IsNullOrEmpty(json.text))
            {
                Debug.LogWarning("[CBPixelFx] asset not found for '" + name + "' (expected " + name
                    + ".png + .json under a Resources folder). Placeholder FX stays active.");
                return null;
            }

            PixelFxSheet sheet;
            try { sheet = JsonUtility.FromJson<PixelFxSheet>(json.text); }
            catch (Exception e)
            {
                Debug.LogError("[CBPixelFx] failed to parse JSON for '" + name + "': " + e.Message);
                return null;
            }
            if (sheet == null || sheet.frames == null || sheet.frames.Count == 0)
            {
                Debug.LogError("[CBPixelFx] '" + name + "' JSON has no frames - skipping.");
                return null;
            }

            int n = sheet.frames.Count;
            var sprites = new Sprite[n];
            var durations = new float[n];
            float fps = (sheet.meta != null && sheet.meta.pixelfx != null) ? sheet.meta.pixelfx.fps : 30f;
            if (fps <= 0f) fps = 30f;

            // pivot is source px, top-left origin; convert to normalized Unity pivot (bottom-left origin).
            float px = 0.5f, py = 0.5f;
            if (sheet.meta != null && sheet.meta.pixelfx != null
                && sheet.meta.pixelfx.pivot != null && sheet.meta.pixelfx.pivot.Length >= 2)
            {
                float fw = (sheet.meta.pixelfx.frameSize != null && sheet.meta.pixelfx.frameSize.Length >= 2
                    && sheet.meta.pixelfx.frameSize[0] > 0f) ? sheet.meta.pixelfx.frameSize[0] : 1f;
                float fh = (sheet.meta.pixelfx.frameSize != null && sheet.meta.pixelfx.frameSize.Length >= 2
                    && sheet.meta.pixelfx.frameSize[1] > 0f) ? sheet.meta.pixelfx.frameSize[1] : 1f;
                px = Mathf.Clamp01(sheet.meta.pixelfx.pivot[0] / fw);
                py = Mathf.Clamp01(1f - sheet.meta.pixelfx.pivot[1] / fh);
            }

            for (int i = 0; i < n; i++)
            {
                var f = sheet.frames[i];
                if (f == null || f.frame == null)
                {
                    sprites[i] = null;
                    durations[i] = 1f / fps;
                    continue;
                }

                // Texture space origin is bottom-left; the sheet JSON measures Y from the top.
                var rect = new Rect(f.frame.x, tex.height - (f.frame.y + f.frame.h), f.frame.w, f.frame.h);
                var sprite = Sprite.Create(tex, rect, new Vector2(px, py), PixelsPerUnit, 0u, SpriteMeshType.FullRect);
                sprite.name = name + "_" + i;
                sprites[i] = sprite;
                durations[i] = f.duration > 0 ? f.duration / 1000f : (1f / fps);
            }

            return new CBPixelFxClip
            {
                Name = name,
                Texture = tex,
                Frames = sprites,
                Durations = durations,
                Fps = fps,
                Loop = sheet.meta != null && sheet.meta.pixelfx != null && sheet.meta.pixelfx.loop,
                Pivot = new Vector2(px, py),
                Columns = sheet.meta != null && sheet.meta.pixelfx != null ? sheet.meta.pixelfx.columns : 1,
            };
        }

        // ---------------- JSON model (Aseprite-style, exactly as tools/render.mjs --sheet emits) ----------------
        // Verified against tools/lib/fxlib.mjs spriteSheet(): frames is an ARRAY of
        // { filename, frame:{x,y,w,h}, rotated, trimmed, spriteSourceSize:{x,y,w,h}, sourceSize:{w,h}, duration(ms) };
        // meta holds size:{w,h}, frameTags[], and pixelfx:{ id, name, fps, loop, frames, frameSize:[w,h], pivot:[x,y], columns }.
        [Serializable]
        public class PixelFxSheet
        {
            public List<FrameEntry> frames;
            public MetaData meta;
        }

        [Serializable]
        public class FrameEntry
        {
            public string filename;
            public FrameRect frame;
            public bool rotated;
            public bool trimmed;
            public FrameRect spriteSourceSize;
            public FrameRect sourceSize;
            public int duration;   // milliseconds
        }

        [Serializable]
        public class FrameRect
        {
            public int x;
            public int y;
            public int w;
            public int h;
        }

        [Serializable]
        public class MetaData
        {
            public string app;
            public string version;
            public string image;
            public string format;
            public SizeData size;
            public string scale;
            public List<FrameTag> frameTags;
            public PixelFxData pixelfx;
        }

        [Serializable]
        public class SizeData
        {
            public int w;
            public int h;
        }

        [Serializable]
        public class FrameTag
        {
            public string name;
            public int from;
            public int to;
            public string direction;
        }

        [Serializable]
        public class PixelFxData
        {
            public string id;
            public string name;
            public float fps;
            public bool loop;
            public int frames;
            public float[] frameSize;   // [w, h]
            public float[] pivot;       // [x, y] source px, top-left origin
            public int columns;
            // "act" is optional and intentionally ignored.
        }
    }

    /// <summary>
    /// A baked pixelfx effect: a grid of FullRect sprites plus per-frame durations and a loop flag.
    /// Built lazily by CBPixelFx from the sheet JSON; consumed by CBFx.SpawnEffect.
    /// </summary>
    public class CBPixelFxClip
    {
        public string Name;
        public Texture2D Texture;
        public Sprite[] Frames;
        public float[] Durations;   // seconds per frame
        public float Fps;
        public bool Loop;
        public Vector2 Pivot;
        public int Columns;

        public float TotalDuration
        {
            get
            {
                if (Durations == null || Durations.Length == 0) return 0f;
                float s = 0f;
                for (int i = 0; i < Durations.Length; i++) s += Durations[i];
                return s;
            }
        }

        /// <summary>Frame index for a given (already looped) local time. Clamps to the last frame.</summary>
        public int FrameIndexAt(float time)
        {
            if (Durations == null || Durations.Length == 0) return 0;
            float t = time;
            for (int i = 0; i < Durations.Length; i++)
            {
                if (t < Durations[i]) return i;
                t -= Durations[i];
            }
            return Durations.Length - 1;
        }
    }
}
