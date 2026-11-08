using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClearToLand.EditorTools
{
    /// <summary>Writes a <see cref="PixelCanvas"/> to a PNG under Assets/ and imports it as a single Sprite.</summary>
    public static class SpriteImportUtil
    {
        /// <param name="assetPath">Project-relative path ending in .png, e.g. Assets/Game/Art/Generated/fx_ring.png</param>
        /// <param name="pixelsPerUnit">Sprite PPU (GameConstants.SpritePixelsPerUnit for world sprites, UiPixelsPerUnit for UI).</param>
        /// <param name="border">9-slice border in pixels (left, bottom, right, top); zero for none.</param>
        /// <param name="compressed">Use platform compression (large backgrounds). Small sprites stay uncompressed for crisp edges.</param>
        public static Sprite SavePngAsSprite(PixelCanvas canvas, string assetPath, float pixelsPerUnit,
            Vector4 border = default, bool compressed = false, FilterMode filter = FilterMode.Bilinear)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            EnsureFolder(folder);
            File.WriteAllBytes(ToFullPath(assetPath), canvas.EncodeToPng());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = pixelsPerUnit;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.sRGBTexture = true;
            imp.filterMode = filter;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = compressed ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(canvas.Width, canvas.Height)), 32, 4096);

            TextureImporterSettings settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = border;
            settings.spriteGenerateFallbackPhysicsShape = false;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) Debug.LogError($"[ClearToLand] Failed to import sprite at {assetPath}");
            return sprite;
        }

        /// <summary>Creates an Assets/... folder (and parents) if missing.</summary>
        public static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(assetFolder);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static string ToFullPath(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
    }
}
