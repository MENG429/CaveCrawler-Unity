using UnityEditor;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Headless validation for the CB art pipeline. Run from CI / batch:
    ///   Unity.exe -batchmode -nographics -quit -projectPath ... -executeMethod CB.CBArtValidation.RunFromCLI
    /// Exits 1 when any sheet fails to load or any import setting drifted from the
    /// pixel-art contract, so a green exit code means "art is wired and crisp".
    /// </summary>
    public static class CBArtValidation
    {
        static int s_failures;

        public static void RunFromCLI()
        {
            Validate();
            Debug.Log(string.Format("[CBArtValidation] done: {0} failure(s)", s_failures));
            EditorApplication.Exit(s_failures > 0 ? 1 : 0);
        }

        [MenuItem("CB/Validate Art Pipeline")]
        static void ValidateMenu()
        {
            Validate();
            Debug.Log(string.Format("[CBArtValidation] done: {0} failure(s)", s_failures));
        }

        static void Validate()
        {
            s_failures = 0;

            for (int i = 0; i < CBArtFrames.Sheets.Length; i++)
            {
                var def = CBArtFrames.Sheets[i];

                string path = "Assets/CB/Resources/CB/Art/" + def.Name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError("[CBArtValidation] missing/invalid importer: " + path);
                    s_failures++;
                    continue;
                }

                if (importer.filterMode != FilterMode.Point)
                    { Debug.LogError("[CBArtValidation] " + def.Name + ": filterMode is " + importer.filterMode); s_failures++; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    { Debug.LogError("[CBArtValidation] " + def.Name + ": compression " + importer.textureCompression); s_failures++; }
                if (importer.mipmapEnabled)
                    { Debug.LogError("[CBArtValidation] " + def.Name + ": mipmaps enabled"); s_failures++; }
                if (importer.textureType != TextureImporterType.Sprite)
                    { Debug.LogError("[CBArtValidation] " + def.Name + ": textureType " + importer.textureType); s_failures++; }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                {
                    Debug.LogError("[CBArtValidation] texture not loadable: " + path);
                    s_failures++;
                    continue;
                }
                if (tex.filterMode != FilterMode.Point)
                    { Debug.LogError("[CBArtValidation] " + def.Name + ": runtime filterMode " + tex.filterMode); s_failures++; }

                // Sprite creation from the frame table (same code path as CBArt.Load).
                int frames = def.Frames != null ? def.Frames.Length : 0;
                Sprite first = null;
                for (int f = 0; f < frames && first == null; f++)
                {
                    var fr = def.Frames[f];
                    var rect = new Rect(fr.X, tex.height - (fr.Y + fr.H), fr.W, fr.H);
                    first = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), def.Ppu, 0u, SpriteMeshType.FullRect);
                }

                int clipCount = def.Clips != null ? def.Clips.Length : 0;
                bool clipsOk = true;
                if (def.Clips != null)
                {
                    foreach (var clip in def.Clips)
                    {
                        if (clip.Start + clip.Count > frames) { clipsOk = false; }
                    }
                }

                Debug.Log(string.Format(
                    "[CBArtValidation] {0}: {1}x{2} frames={3} clips={4} ppu={5} firstSize={6} clipsInRange={7}",
                    def.Name, tex.width, tex.height, frames, clipCount, def.Ppu,
                    first != null ? first.bounds.size.ToString("F2") : "n/a", clipsOk));

                if (!clipsOk) { Debug.LogError("[CBArtValidation] " + def.Name + ": clip range out of bounds"); s_failures++; }
                if (frames == 0 && def.Name != "ui_hud" && def.Name != "deco_cave")
                    { Debug.LogError("[CBArtValidation] " + def.Name + ": zero frames in table"); s_failures++; }
            }

            // Through the public API too (lazy Resources path).
            CBArt.Clip clip2;
            bool playerOk = CBArt.TryGetClip("player_idle_run", "run", out clip2);
            Debug.Log(string.Format("[CBArtValidation] API probe player_idle_run/run -> {0} ({1} frames)",
                playerOk, playerOk && clip2.Frames != null ? clip2.Frames.Length : 0));
            if (!playerOk) s_failures++;
        }
    }
}
