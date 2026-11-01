using UnityEditor;
using UnityEngine;

namespace CB
{
    /// <summary>
    /// Enforces pixel-art import settings for the baked pixelfx PNGs under
    /// Assets/CB/External/pixelfx/Resources, mirroring CBArtImporter:
    ///   * Sprite / point filter / no compression / no mipmaps  -> crisp transparent pixels
    ///   * NPOT None + max 2048                              -> sheets stay byte-exact
    /// The paired .json files import as TextAssets untouched. Real sprites are created at runtime
    /// by CBPixelFx from the measured rects (Sprite.Create, FullRect), so the editor sprite slicer
    /// is not used and spritePixelsPerUnit here is nominal only.
    /// </summary>
    public class CBPixelFxImporter : AssetPostprocessor
    {
        const string Dir = "Assets/CB/External/pixelfx/Resources/";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Dir)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;                 // never bilinear on pixel art
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 16f;                    // matches game art PPU; runtime sprites set their own PPU
        }

        [MenuItem("CB/Reimport PixelFx")]
        static void ReimportAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Dir.TrimEnd('/') });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();
        }
    }
}
