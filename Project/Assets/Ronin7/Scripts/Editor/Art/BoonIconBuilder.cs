using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// P5-07: turns the AI boon icons (white neon line-art on black, no alpha) into 256² white sprites with
    /// alpha = luminance (max r,g,b), so panels tint them by rarity via Image.color. Sources stay in
    /// <c>BoonIcons/</c>; processed sprites go to <c>BoonIcons/Processed/</c>, packed by one V2 SpriteAtlas.
    /// Android ASTC 4x4, no mips. Idempotent (overwrites in place).
    /// </summary>
    public static class BoonIconBuilder
    {
        public const string SourceFolder = "Assets/Ronin7/Art/UI/BoonIcons";
        public const string ProcessedFolder = SourceFolder + "/Processed";
        public const string AtlasPath = SourceFolder + "/BoonIcons.spriteatlasv2";
        private const int Size = 256;

        [MenuItem("Tools/Space Samurai/Art/Build Boon Icons")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(ProcessedFolder)) AssetDatabase.CreateFolder(SourceFolder, "Processed");

            int n = 0;
            foreach (var src in Directory.GetFiles(SourceFolder, "Icon_*.png"))
            {
                string name = Path.GetFileName(src);
                if (name.Contains(" ")) continue; // "Icon_X 1.png" variants are not sources
                File.WriteAllBytes(Path.Combine(ProcessedFolder, name), Process(File.ReadAllBytes(src)).EncodeToPNG());
                n++;
            }
            AssetDatabase.Refresh();

            foreach (var path in Directory.GetFiles(ProcessedFolder, "Icon_*.png"))
                ConfigureSprite(path.Replace('\\', '/'));

            BakeUiFrame();
            EnsureAtlas();
            AssetDatabase.SaveAssets();
            Debug.Log($"[BoonIconBuilder] processed {n} icon(s) into {ProcessedFolder}, atlas {AtlasPath}");
        }

        /// <summary>P5-07d (A13.7): the UiKit 9-slice frame as ONE sprite — rounded dark-glass fill (texel ~16/255,
        /// so a cyan tint reads as the old near-black glass) with a 3 px white neon edge — saved under
        /// <c>Processed/Resources</c> so it packs into the boon atlas (frames + icons share a texture in builds) and
        /// UiKit can load it by name.</summary>
        [MenuItem("Tools/Space Samurai/Art/Bake UiKit Frame")]
        public static void BakeUiFrame()
        {
            const int cell = 32, border = 12; const float radius = 10f, edgeWidth = 3f; const byte fill = 16;
            string folder = ProcessedFolder + "/Resources";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(ProcessedFolder, "Resources");
            var px = new Color32[cell * cell];
            for (int y = 0; y < cell; y++)
            for (int x = 0; x < cell; x++)
            {
                float d = Ronin7.Flow.UiKit.RoundBoxDistance(x + 0.5f - cell * 0.5f, y + 0.5f - cell * 0.5f, cell * 0.5f - 0.5f, radius);
                float a = Mathf.Clamp01(0.5f - d), e = Mathf.Clamp01(d + edgeWidth + 0.5f);
                byte v = (byte)Mathf.RoundToInt(Mathf.Lerp(fill, 255f, e));
                px[y * cell + x] = new Color32(v, v, v, (byte)Mathf.RoundToInt(a * 255f));
            }
            var tex = new Texture2D(cell, cell, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            string path = folder + "/" + Ronin7.Flow.UiKit.FrameSprite + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(ts);
            imp.SaveAndReimport();
        }

        /// <summary>Box-downsample to 256² and move luminance into alpha (colour white).</summary>
        internal static Texture2D Process(byte[] png)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(png);
            var s = src.GetPixels32();
            int sw = src.width, sh = src.height;
            var dst = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int x0 = x * sw / Size, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / Size);
                int y0 = y * sh / Size, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / Size);
                float sum = 0; int cnt = 0;
                for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                {
                    var c = s[yy * sw + xx];
                    sum += Mathf.Max(c.r, Mathf.Max(c.g, c.b)) * (c.a / 255f);
                    cnt++;
                }
                // Clip the near-black background noise to fully transparent.
                float a = Mathf.Clamp01((sum / cnt / 255f - 0.04f) / 0.96f);
                dst[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            Object.DestroyImmediate(src);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(dst);
            tex.Apply();
            return tex;
        }

        private static void ConfigureSprite(string path)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.maxTextureSize = Size;
            imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = Size, format = TextureImporterFormat.ASTC_4x4,
            });
            imp.SaveAndReimport();
        }

        private static void EnsureAtlas()
        {
            if (!File.Exists(AtlasPath))
            {
                var asset = new SpriteAtlasAsset();
                asset.Add(new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(ProcessedFolder) });
                SpriteAtlasAsset.Save(asset, AtlasPath);
                AssetDatabase.ImportAsset(AtlasPath);
            }
            var imp = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
            imp.includeInBuild = true;
            imp.packingSettings = new SpriteAtlasPackingSettings
            {
                enableRotation = false, enableTightPacking = false, padding = 4, enableAlphaDilation = true,
            };
            imp.textureSettings = new SpriteAtlasTextureSettings
            {
                generateMipMaps = false, filterMode = FilterMode.Bilinear, sRGB = true, readable = false,
            };
            imp.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.ASTC_4x4,
            });
            imp.SaveAndReimport();

            // Atlases only pack when the V2 packer is on; "enabled for builds" keeps editor iteration cheap.
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;
        }
    }
}
