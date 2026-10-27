using System.Collections.Generic;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Runtime loader for the pixel-art sheets in Assets/CB/Resources/CB/Art.
    ///
    /// DESIGN NOTES
    ///  - The AI-generated sheets are NOT on a strict grid, so CBArtFrames carries explicit,
    ///    measured pixel rects (produced offline by alpha-projection analysis of each sheet).
    ///    At load time every rect becomes a Sprite via Sprite.Create (SpriteMeshType.FullRect
    ///    as required by the project constraints). Nothing depends on the Unity sprite slicer.
    ///  - Sheets load LAZILY: the Texture2D is pulled via Resources.Load on first use and
    ///    cached. A missing/broken asset degrades to "clip not found", and every caller
    ///    keeps its placeholder-colour rendering path - the game stays fully playable.
    ///  - Import settings for these PNGs are enforced by Assets/CB/Editor/CBArtImporter.cs
    ///    (point filter, no compression, no mipmaps) so the pixels stay crisp.
    /// </summary>
    public static class CBArt
    {
        public class Clip
        {
            public string Name;
            public Sprite[] Frames;
            public float Fps;
            public bool Loop;
            /// <summary>World-space size of the largest frame at the sheet's PPU (before scaling).</summary>
            public float WorldSize;
        }

        class Sheet
        {
            public CBArtFrames.SheetDef Def;
            public bool Attempted;
            public Sprite[] Frames;                    // null when load failed
            public Dictionary<string, Clip> Clips;     // null when load failed
        }

        static readonly Dictionary<string, Sheet> s_sheets = new Dictionary<string, Sheet>();
        static bool s_warnedMissing;

        /// <summary>True when at least one sheet loaded - cheap global "is art in" probe.</summary>
        public static bool AnyLoaded
        {
            get
            {
                foreach (var kv in s_sheets)
                {
                    if (kv.Value.Frames != null) return true;
                }
                return false;
            }
        }

        public static void Warmup()
        {
            for (int i = 0; i < CBArtFrames.Sheets.Length; i++)
            {
                Load(CBArtFrames.Sheets[i].Name);
            }
        }

        public static bool TryGetClip(string sheetName, string clipName, out Clip clip)
        {
            clip = null;
            var sheet = Load(sheetName);
            if (sheet == null || sheet.Clips == null) return false;
            return sheet.Clips.TryGetValue(clipName ?? "", out clip) && clip.Frames != null && clip.Frames.Length > 0;
        }

        public static bool FacesLeft(string sheetName)
        {
            var sheet = Load(sheetName);
            return sheet != null && sheet.Def.FacesLeft;
        }

        /// <summary>
        /// Weapon display sprite (pedestal icons, dropped weapons). Known ids map to their
        /// authored frames; unknown ids pick deterministically from the sheet by id hash so
        /// every weapon still shows consistent art. Null when art is unavailable.
        /// </summary>
        public static Clip WeaponClip(CBWeaponDef weapon)
        {
            if (weapon == null) return null;

            bool isBow = weapon is CBBowDef;
            string sheetName = isBow ? "weapon_bows" : "weapon_melee";

            string known = null;
            if (isBow)
            {
                switch (weapon.Id)
                {
                    case "bow_recurve": known = "w_recurve"; break;
                    case "bow_bomb": known = "w_bomb"; break;
                    case "bow_hand_cannon": known = "w_cannon"; break;
                    case "bow_poison": known = "w_poison"; break;
                    case "bow_ice": known = "w_ice"; break;
                }
            }
            else
            {
                switch (weapon.Id)
                {
                    case "sword_spiked_stick": known = "w_sword"; break;
                    case "sword_skull_stick": known = "w_dark"; break;
                    case "sword_orc_battleaxe": known = "w_axe"; break;
                    case "sword_explosion": known = "w_axe"; break;
                }
            }

            if (known != null)
            {
                Clip clip;
                return TryGetClip(sheetName, known, out clip) ? clip : null;
            }

            // Deterministic fallback for weapons without authored art.
            var sheet = Load(sheetName);
            if (sheet == null || sheet.Clips == null || sheet.Clips.Count == 0) return null;
            int hash = 0;
            for (int i = 0; i < weapon.Id.Length; i++) hash = hash * 31 + weapon.Id[i];
            var names = new List<string>(sheet.Clips.Keys);
            names.Sort();
            int idx = ((hash % names.Count) + names.Count) % names.Count;   // positive modulo
            return sheet.Clips[names[idx]];
        }

        /// <summary>
        /// First frame of a clip as a static sprite (icons, projectiles, tiles).
        /// Returns null when art is unavailable - callers must keep their placeholder path.
        /// </summary>
        public static Sprite FirstSprite(string sheetName, string clipName)
        {
            Clip clip;
            return TryGetClip(sheetName, clipName, out clip) ? clip.Frames[0] : null;
        }

        static Sheet Load(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return null;

            Sheet sheet;
            if (s_sheets.TryGetValue(sheetName, out sheet)) return sheet;

            sheet = new Sheet();
            s_sheets[sheetName] = sheet;

            for (int i = 0; i < CBArtFrames.Sheets.Length; i++)
            {
                if (CBArtFrames.Sheets[i].Name == sheetName)
                {
                    sheet.Def = CBArtFrames.Sheets[i];
                    break;
                }
            }
            if (sheet.Def.Name == null)
            {
                WarnOnce("CBArt: no frame table for sheet '" + sheetName + "'");
                return sheet;
            }

            sheet.Attempted = true;

            var tex = Resources.Load<Texture2D>("CB/Art/" + sheetName);
            if (tex == null)
            {
                WarnOnce("CBArt: texture not found (Resources/CB/Art/" + sheetName + ") - placeholder art stays active");
                return sheet;
            }

            int n = sheet.Def.Frames != null ? sheet.Def.Frames.Length : 0;
            if (n == 0) return sheet;

            sheet.Frames = new Sprite[n];
            float ppu = Mathf.Max(1f, sheet.Def.Ppu);
            float maxSize = 0f;

            for (int i = 0; i < n; i++)
            {
                var f = sheet.Def.Frames[i];
                // Table Y is measured from the top; Unity rects start at the bottom.
                var rect = new Rect(f.X, tex.height - (f.Y + f.H), f.W, f.H);
                var sprite = Sprite.Create(
                    tex,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    ppu,
                    0u,
                    SpriteMeshType.FullRect);
                sprite.name = sheetName + "_" + i;
                sheet.Frames[i] = sprite;

                float ext = Mathf.Max(f.W, f.H) / ppu;
                if (ext > maxSize) maxSize = ext;
            }

            sheet.Clips = new Dictionary<string, Clip>();
            var clips = sheet.Def.Clips;
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    var def = clips[i];
                    if (def.Start + def.Count > n) continue;   // out-of-range clip: skip, never throw

                    var clip = new Clip
                    {
                        Name = def.Name,
                        Fps = Mathf.Max(1f, def.Fps),
                        Loop = def.Loop,
                        WorldSize = maxSize,
                        Frames = new Sprite[def.Count]
                    };
                    for (int k = 0; k < def.Count; k++)
                    {
                        clip.Frames[k] = sheet.Frames[def.Start + k];
                        if (clip.Frames[k] == null) { clip.Frames = null; break; }
                    }
                    if (clip.Frames != null) sheet.Clips[def.Name] = clip;
                }
            }

            return sheet;
        }

        static void WarnOnce(string message)
        {
            if (s_warnedMissing) return;
            s_warnedMissing = true;
            Debug.LogWarning(message + " (further CBArt load warnings suppressed)");
        }
    }

    /// <summary>
    /// Minimal frame animator bound to one SpriteRenderer. Ticked manually from the owner's
    /// FixedStep so animation follows the deterministic clock. When no clip is playing the
    /// renderer keeps whatever placeholder state the caller set up.
    /// </summary>
    public class CBAnimPlayer
    {
        readonly SpriteRenderer m_sr;
        CBArt.Clip m_clip;
        float m_time;
        int m_index = -1;

        public bool Valid { get { return m_clip != null && m_clip.Frames != null && m_clip.Frames.Length > 0; } }
        public CBArt.Clip Current { get { return m_clip; } }

        public CBAnimPlayer(SpriteRenderer sr)
        {
            m_sr = sr;
            if (m_sr != null)
            {
                // Art sprites carry their own size - Sliced draw mode and the placeholder
                // tint no longer apply once this player takes over.
                m_sr.drawMode = SpriteDrawMode.Simple;
                m_sr.color = Color.white;
            }
        }

        /// <summary>Switch clips. Same-clip calls without restart are no-ops (safe to call every step).</summary>
        public void Play(CBArt.Clip clip, bool restart)
        {
            if (clip == null || clip.Frames == null || clip.Frames.Length == 0) return;
            if (m_sr == null) return;
            if (!restart && m_clip == clip) return;

            m_clip = clip;
            m_time = 0f;
            m_index = -1;
        }

        public void Tick(float dt)
        {
            if (!Valid || m_sr == null) return;

            m_time += dt;
            int count = m_clip.Frames.Length;
            int idx = m_clip.Loop
                ? (int)(m_time * m_clip.Fps) % count
                : Mathf.Min((int)(m_time * m_clip.Fps), count - 1);

            if (idx == m_index) return;
            m_index = idx;
            m_sr.sprite = m_clip.Frames[idx];
        }
    }
}
