using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Outfit, the Liquid Glass typeface: static SDF font assets, Regular and
    /// Bold, made here from the two TTFs in Assets/UI/Fonts. Those are
    /// instances of Google Fonts' Outfit[wght].ttf (OFL) at 400 and 700,
    /// because the variable file's default instance is Thin and Unity's font
    /// engine would only ever draw that.
    ///
    /// Bold is Regular's weight-table entry for 700, so a &lt;b&gt; tag or
    /// FontStyles.Bold draws the real bold cut rather than a dilated Regular.
    /// Outfit has no ♯ ▲ ▼: those come from LiberationSans through the
    /// fallback table.
    ///
    /// Static atlases: nothing rasterises in the headset. A character outside
    /// <see cref="Characters"/> falls back to LiberationSans too; add it here
    /// and delete the two .asset files to rebuild them.
    /// </summary>
    public static class GlassFont
    {
        private const string Folder = "Assets/UI/Fonts/";
        public const string RegularPath = Folder + "Outfit SDF.asset";
        public const string BoldPath = Folder + "Outfit Bold SDF.asset";
        private const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private const int SamplingSize = 72, Padding = 8, AtlasSize = 1024;

        private static string Characters
        {
            get
            {
                var chars = new char[95];
                for (int i = 0; i < chars.Length; i++) chars[i] = (char)(32 + i);   // printable ASCII
                return new string(chars) + "·–—°×±’“”…";
            }
        }

        /// <summary>The Regular asset, with Bold wired in; built if missing.</summary>
        public static TMP_FontAsset GetOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularPath);
            if (existing != null) return existing;

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            TMP_FontAsset regular = Build("Outfit-Regular.ttf", RegularPath, fallback);
            TMP_FontAsset bold = Build("Outfit-Bold.ttf", BoldPath, fallback);
            if (regular == null || bold == null) return null;

            regular.fontWeightTable[7].regularTypeface = bold;
            EditorUtility.SetDirty(regular);
            AssetDatabase.SaveAssets();
            return regular;
        }

        private static TMP_FontAsset Build(string ttf, string path, TMP_FontAsset fallback)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + ttf);
            if (source == null)
            {
                Debug.LogError($"[GlassFont] {Folder}{ttf} not found.");
                return null;
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, SamplingSize, Padding, GlyphRenderMode.SDFAA,
                                                               AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (font == null)
            {
                Debug.LogError($"[GlassFont] could not create a font asset from {ttf}.");
                return null;
            }

            font.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[GlassFont] {ttf} lacks: {missing}");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            if (fallback != null) font.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            font.name = name;
            font.atlasTexture.name = name + " Atlas";
            font.material.name = name + " Material";
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GlassFont] built {path}: {font.characterTable.Count} characters.");
            return font;
        }
    }
}
