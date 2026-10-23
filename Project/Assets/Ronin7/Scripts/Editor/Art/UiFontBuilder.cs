using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Ronin7.Editor.Art
{
    /// <summary>
    /// P5-07 (A13.3): bakes the Rajdhani TMP font assets (SDF, 1024 atlas, padding 9, static) with
    /// LiberationSans SDF as fallback. Idempotent: an existing asset is left in place (stable GUID —
    /// panels and TMP Settings reference it); delete it by hand to force a re-bake. P5-07b: also maps
    /// SemiBold's Bold (700) weight slot to Rajdhani-Bold and makes SemiBold the TMP default font, so
    /// runtime-built panels get Rajdhani body text and real Rajdhani Bold for FontStyles.Bold with no
    /// asset loading at runtime.
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
            ConfigureDefaults();
            AssetDatabase.SaveAssets();
        }

        private static void Bake(string ttfPath, string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null)
            {
                Debug.Log($"[UiFontBuilder] {assetPath} exists — kept (GUID stable).");
                return;
            }
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

            AssetDatabase.CreateAsset(fa, assetPath);
            var atlas = fa.atlasTextures[0];
            atlas.name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            Debug.Log($"[UiFontBuilder] {assetPath}: {fa.characterTable.Count} chars");
        }
    
        /// <summary>SemiBold weight 700 → Rajdhani-Bold; TMP Settings default font → SemiBold.</summary>
        internal static void ConfigureDefaults()
        {
            var semi = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiBoldPath);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
            if (semi == null || bold == null) { Debug.LogError("[UiFontBuilder] fonts missing — cannot configure defaults"); return; }

            var so = new SerializedObject(semi);
            so.FindProperty("m_FontWeightTable").GetArrayElementAtIndex(7).FindPropertyRelative("regularTypeface").objectReferenceValue = bold;
            so.ApplyModifiedPropertiesWithoutUndo();

            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = semi;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(TMP_Settings.instance);
        }
    }
}
