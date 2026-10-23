using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// P5-07 (A13.3): bakes the Rajdhani TMP font assets (SDF, 1024 atlas, padding 9, static) with
    /// LiberationSans SDF as fallback. Idempotent: re-running deletes and re-creates the two assets
    /// in place (same path; GUIDs change, so re-point panels by path, not by cached reference).
    /// </summary>
    public static class UiFontBuilder
    {
        private const string FontsFolder = "Assets/Ronin7/Art/UI/Fonts";
        private const string Fallback = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        public const string SemiBoldPath = FontsFolder + "/Rajdhani-SemiBold SDF.asset";
        public const string BoldPath = FontsFolder + "/Rajdhani-Bold SDF.asset";

        /// <summary>ASCII 0x20-0x7E, Latin-1 0xA0-0xFF, plus the extra punctuation the UI uses.</summary>
        internal static string Charset()
        {
            var sb = new System.Text.StringBuilder();
            for (char c = (char)0x20; c <= 0x7E; c++) sb.Append(c);
            for (char c = (char)0xA0; c <= 0xFF; c++) sb.Append(c);
            sb.Append("–—‘’“”•…→←↑↓™€");
            return sb.ToString();
        }

        [MenuItem("Tools/Space Samurai/Art/Build UI Fonts")]
        public static void Build()
        {
            Bake(FontsFolder + "/Rajdhani/Rajdhani-SemiBold.ttf", SemiBoldPath);
            Bake(FontsFolder + "/Rajdhani/Rajdhani-Bold.ttf", BoldPath);
            AssetDatabase.SaveAssets();
        }

        private static void Bake(string ttfPath, string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) { Debug.LogError($"[UiFontBuilder] missing {ttfPath}"); return; }

            var fa = TMP_FontAsset.CreateFontAsset(font, 56, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            fa.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            fa.TryAddCharacters(Charset(), out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[UiFontBuilder] {fa.name} missing glyphs: {missing}");
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            fa.fallbackFontAssetTable = new List<TMP_FontAsset> { AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fallback) };

            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            var atlas = fa.atlasTextures[0];
            atlas.name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            Debug.Log($"[UiFontBuilder] {assetPath}: {fa.characterTable.Count} chars");
        }
    }
}
