using TMPro;
using UnityEditor;
using UnityEngine;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// The text material every glass surface uses: the label font's own SDF
    /// material plus a soft dark underlay. Liquid Glass shows the room
    /// through it, and over a bright window light text alone loses its edge
    /// -- the design calls for "white text with a subtle shadow". One asset
    /// (Assets/UI/GlassText.mat), made here, shared by SceneSetup and the
    /// glass preview so both show the same thing. TMP gives the fallback
    /// atlas (♯, ▲, ▼) a matching underlay itself.
    /// </summary>
    public static class GlassTextMaterial
    {
        public const string AssetPath = "Assets/UI/GlassText.mat";

        public static Material GetOrCreate(TMP_FontAsset font)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(AssetPath);
            if (existing != null) return existing;
            if (font == null || font.material == null) return null;

            var mat = new Material(font.material) { name = "GlassText" };
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            mat.SetFloat("_UnderlayOffsetX", 0.35f);
            mat.SetFloat("_UnderlayOffsetY", -0.5f);
            mat.SetFloat("_UnderlayDilate", 0.15f);
            mat.SetFloat("_UnderlaySoftness", 0.55f);

            if (!AssetDatabase.IsValidFolder("Assets/UI")) AssetDatabase.CreateFolder("Assets", "UI");
            AssetDatabase.CreateAsset(mat, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GlassText] created {AssetPath}");
            return mat;
        }
    }
}
