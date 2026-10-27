using UnityEditor;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Enforces pixel-art import settings for every PNG under Assets/CB/Art (source sheets)
    /// and Assets/CB/Resources/CB/Art (cleaned runtime sheets):
    ///   * Sprite / point filter / no compression / no mipmaps  -> crisp pixels, no blur
    ///   * NPOT None + max 2048                                  -> sheets stay byte-exact
    ///   * spritePixelsPerUnit is nominal only - real sprites are created at runtime by
    ///     CBArt from the measured rects in CBArtFrames, so the sprite slicer is not used.
    ///     Single mode (not Multiple) is deliberate: Multiple with no defined rects yields
    ///     zero sub-sprites, and runtime rect slicing makes editor-side slicing redundant.
    /// Runs in batch mode too (AssetPostprocessor is invoked on every import, including
    /// -batchmode -nographics -quit runs).
    /// </summary>
    public class CBArtImporter : AssetPostprocessor
    {
        const string SourceDir = "Assets/CB/Art/";
        const string RuntimeDir = "Assets/CB/Resources/CB/Art/";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(SourceDir) && !path.StartsWith(RuntimeDir)) return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;                 // never bilinear on pixel art
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 16f;                     // 1 tile = 16px reference
            importer.wrapMode = System.IO.Path.GetFileNameWithoutExtension(path) == "bg_parallax"
                ? TextureWrapMode.Repeat
                : TextureWrapMode.Clamp;
        }

        [MenuItem("CB/Reimport CB Art")]
        static void ReimportAll()
        {
            ReimportFolder("Assets/CB/Art");
            ReimportFolder(RuntimeDir.TrimEnd('/'));
            AssetDatabase.Refresh();
        }

        static void ReimportFolder(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
