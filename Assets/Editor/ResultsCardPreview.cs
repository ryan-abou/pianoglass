using System;
using System.IO;
using PianoGlass.Scoring;
using PianoGlass.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Renders the real ResultsCard, filled with each ResultsSamples entry,
    /// to PNGs: closed and with the details panel open. Same compositing as
    /// GlassPreview. Saves nothing to the scene: it works in a throwaway one.
    /// Output goes to Logs/results-preview, or -resultsOut. -glassBackdrop
    /// is optional.
    ///
    /// To see it in the Game view instead: enter play mode, select the
    /// scene's ResultsCard, and use the "Sample: ..." items in its context menu.
    /// </summary>
    public static class ResultsCardPreview
    {
        private const string TokensPath = "Assets/UI/GlassTokens.asset";
        private const string MaterialPath = "Assets/LiquidGlass.mat";

        [MenuItem("PianoGlass/Preview/Render Results Card Samples")]
        public static void RenderAll()
        {
            int exit = 0;
            try
            {
                string folder = GlassPreview.Arg("-resultsOut") ?? "Logs/results-preview";
                string backdrop = GlassPreview.Arg("-glassBackdrop");
                foreach (string sample in ResultsSamples.All)
                {
                    string slug = sample.ToLowerInvariant().Replace(' ', '-').Replace(",", "");
                    RenderOne(sample, false, backdrop, Path.Combine(folder, slug + "-closed.png"));
                    RenderOne(sample, true, backdrop, Path.Combine(folder, slug + "-open.png"));
                }
                Debug.Log($"[ResultsPreview] wrote {Path.GetFullPath(folder)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResultsPreview] failed: {e}");
                exit = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        private static void RenderOne(string sample, bool open, string backdrop, string output)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            camGo.transform.position = new Vector3(0.13f, -0.03f, -1.05f);

            var tokens = AssetDatabase.LoadAssetAtPath<GlassTokens>(TokensPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            TMP_FontAsset font = GlassFont.GetOrCreate();
            Material text = GlassTextMaterial.GetOrCreate(font);

            var go = new GameObject("ResultsCard", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            go.transform.localScale = Vector3.one * 0.001f;

            var glass = go.AddComponent<GlassPanel>();
            glass.Init(material, tokens, GlassStyle.Panel, Vector2.zero, 0.002f);

            var card = go.AddComponent<ResultsCard>();
            card.UsePreviewAssets(tokens, font, text, material);
            card.EnsureBuilt();
            card.Show(ResultsSamples.Summary(sample));
            card.SetDetailsInstant(open);

            foreach (GlassPanel g in go.GetComponentsInChildren<GlassPanel>(true)) g.RebuildNow();
            foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();

            GlassPreview.CaptureOver(cam, backdrop, output);
        }
    }
}
