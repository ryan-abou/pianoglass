using System;
using System.IO;
using PianoGlass.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Renders Liquid Glass to a PNG without a headset, the way the Quest
    /// would show it: the glass and text are drawn by URP into a transparent
    /// buffer, then composited over a room photo exactly as the Quest
    /// compositor puts the eye buffer over passthrough --
    /// rgb + room * (1 - alpha), in linear light. Use a greyscale photo:
    /// Quest 2 passthrough is monochrome.
    ///
    /// A sample layout (a card, a nested row, chips and heat cells) is built
    /// in a throwaway scene from GlassPanel's own BuildQuad/Apply, so what
    /// is previewed is what ships. Tune GlassTokens.asset, re-run.
    ///
    ///   Unity.exe -batchmode -quit -projectPath ... -buildTarget Android
    ///     -executeMethod PianoGlass.EditorTools.GlassPreview.Render
    ///     -glassBackdrop room.png -glassOut glass.png [-glassYaw 20]
    ///
    /// Not -nographics: it needs a GPU device to render.
    /// </summary>
    public static class GlassPreview
    {
        private const string TokensPath = "Assets/UI/GlassTokens.asset";
        private const string MaterialPath = "Assets/LiquidGlass.mat";
        private const string ShaderPath = "Assets/Shaders/LiquidGlass.shader";
        private const int Width = 1920, Height = 1080;
        private const float Mm = 0.001f;
        private static Material _textMaterial;

        [MenuItem("PianoGlass/Preview/Render Glass Preview")]
        public static void Render()
        {
            int exit = 0;
            try
            {
                string backdrop = Arg("-glassBackdrop");
                string output = Arg("-glassOut") ?? "Logs/glass-preview.png";
                float yaw = float.TryParse(Arg("-glassYaw"), out float y) ? y : 0f;
                RenderTo(backdrop, output, yaw);
                Debug.Log($"[GlassPreview] wrote {Path.GetFullPath(output)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GlassPreview] failed: {e}");
                exit = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        private static void RenderTo(string backdropPath, string output, float yawDegrees)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var tokens = AssetDatabase.LoadAssetAtPath<GlassTokens>(TokensPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
            TMP_FontAsset font = GlassFont.GetOrCreate();
            _textMaterial = GlassTextMaterial.GetOrCreate(font);
            Color primary = tokens != null ? tokens.textPrimary : GlassTokens.DefaultTextPrimary;
            Color secondary = tokens != null ? tokens.textSecondary : GlassTokens.DefaultTextSecondary;

            var camGo = new GameObject("PreviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.allowHDR = false;
            cam.allowMSAA = true;
            camGo.transform.position = new Vector3(0f, 0f, -0.75f);

            // The card, in canvas millimetres like the real ones; turned by
            // yawDegrees so the view-dependent highlights can be checked.
            var root = new GameObject("Card").transform;
            root.localScale = Vector3.one * Mm;
            root.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            // Card 460 x 270, as ResultsCard.
            Glass(root, material, tokens, GlassStyle.Panel, new Vector2(0, 0), new Vector2(460, 270), 2.0f);
            Text(root, font, "Twinkle, Twinkle, Little Star  ·  Tempo 85%", 17, secondary, new Vector2(-208, 113), TextAlignmentOptions.TopLeft, 420);
            Text(root, font, "<b>92%</b>", 76, primary, new Vector2(-208, 92), TextAlignmentOptions.TopLeft, 220);
            Text(root, font, "Great", 30, primary, new Vector2(-10, 46), TextAlignmentOptions.TopLeft, 200);

            // A nested row (iOS grouped style): three items, label over
            // value, split by hairline dividers -- no borders of their own.
            Glass(root, material, tokens, GlassStyle.Row, new Vector2(0, -22), new Vector2(424, 50), 1.4f);
            string[] labels = { "vs last", "timing", "wrong notes" };
            string[] values = { "▲ 6%", "▼ 37 ms late", "2" };
            for (int i = 0; i < 3; i++)
            {
                float cx = -141 + i * 141;
                Text(root, font, labels[i], 12, secondary, new Vector2(cx - 70, -3), TextAlignmentOptions.Top, 140);
                Text(root, font, values[i], 19, primary, new Vector2(cx - 70, -18), TextAlignmentOptions.Top, 140);
                if (i > 0)
                    Glass(root, material, tokens, GlassStyle.Pill, new Vector2(cx - 70.5f, -22), new Vector2(0.8f, 30),
                          1.0f, new Color(1f, 1f, 1f, 0.22f));
            }

            // Heat strip: 8 pill cells tinted on the score ramp.
            float[] acc = { 100, 96, 62, 88, 100, 92, 78, 100 };
            for (int i = 0; i < acc.Length; i++)
            {
                float cx = -184 + i * 52.6f;
                Color c = Ramp(acc[i]);
                c.a = 0.85f;
                Glass(root, material, tokens, GlassStyle.Pill, new Vector2(cx, -78), new Vector2(44, 24), 0.8f, c);
                Text(root, font, (i + 1).ToString(), 12, secondary, new Vector2(cx - 22, -94), TextAlignmentOptions.Top, 44);
            }
            Text(root, font, "weakest: bar 3 (62%)", 16, secondary, new Vector2(-208, -109), TextAlignmentOptions.TopLeft, 300);

            // A pill button off the card, as on the song panel.
            var button = new GameObject("Button").transform;
            button.localScale = Vector3.one * Mm;
            button.position = new Vector3(0f, -0.185f, -0.01f);
            button.rotation = root.rotation;
            Glass(button, material, tokens, GlassStyle.Pill, Vector2.zero, new Vector2(150, 40), 0f);
            Text(button, font, "Retry", 18, primary, new Vector2(-75, 11), TextAlignmentOptions.Top, 150);

            foreach (TMP_Text t in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                t.ForceMeshUpdate();

            CaptureOver(cam, backdropPath, output);
        }

        /// <summary>Renders the camera with a transparent background and
        /// composites it over the backdrop as the Quest does.</summary>
        internal static void CaptureOver(Camera cam, string backdropPath, string output)
        {
            // Render, transparent.
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 4
            };
            cam.targetTexture = rt;
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            Color[] app = Read(rt);
            Color[] room = LoadBackdrop(backdropPath);

            // The Quest compositor: premultiplied eye buffer over passthrough,
            // in linear light.
            var outTex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var pixels = new Color[app.Length];
            for (int i = 0; i < app.Length; i++)
            {
                Color a = app[i];
                Color b = room[i];
                float r = a.r.GammaToLinear() + b.r.GammaToLinear() * (1f - a.a);
                float g = a.g.GammaToLinear() + b.g.GammaToLinear() * (1f - a.a);
                float bl = a.b.GammaToLinear() + b.b.GammaToLinear() * (1f - a.a);
                pixels[i] = new Color(r.LinearToGamma(), g.LinearToGamma(), bl.LinearToGamma(), 1f);
            }
            outTex.SetPixels(pixels);
            outTex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllBytes(output, outTex.EncodeToPNG());

            cam.targetTexture = null;
            rt.Release();
        }

        private static void Glass(Transform parent, Material material, GlassTokens tokens, GlassStyle style,
                                  Vector2 centreMm, Vector2 sizeMm, float depthBehindMm, Color? tint = null)
        {
            var go = new GameObject($"Glass_{style}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(centreMm.x, centreMm.y, depthBehindMm);
            var mesh = new Mesh();
            Vector2 half = sizeMm * 0.5f * Mm;
            GlassPanel.BuildQuad(mesh, half, Mm);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            GlassPanel.Apply(block, tokens, style, half, tint);
            renderer.SetPropertyBlock(block);
        }

        private static void Text(Transform parent, TMP_FontAsset font, string text, float size, Color colour,
                                 Vector2 topLeftMm, TextAlignmentOptions align, float widthMm)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            if (_textMaterial != null) tmp.fontSharedMaterial = _textMaterial;
            tmp.text = text;
            tmp.fontSize = size * 10f;   // a 3D TextMeshPro's size is in tenths of a unit
            tmp.color = colour;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.pivot = new Vector2(0f, 1f);
            tmp.rectTransform.sizeDelta = new Vector2(widthMm, size * 1.6f);
            go.transform.localPosition = new Vector3(topLeftMm.x, topLeftMm.y, -0.5f);
        }

        private static Color[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex.GetPixels();
        }

        private static Color[] LoadBackdrop(string path)
        {
            var result = new Color[Width * Height];
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                for (int i = 0; i < result.Length; i++) result[i] = new Color(0.45f, 0.45f, 0.45f, 1f);
                return result;
            }
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(path));
            var rt = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            result = Read(rt);
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        /// <summary>The heat strip's ramp (coral 0%, amber 70%, mint 100%),
        /// for the sample cells only.</summary>
        private static Color Ramp(float accuracy)
        {
            Color coral = new Color(1f, 0.42f, 0.36f), amber = new Color(0.95f, 0.76f, 0.31f), mint = new Color(0.24f, 0.86f, 0.59f);
            return accuracy <= 70f ? Color.Lerp(coral, amber, accuracy / 70f) : Color.Lerp(amber, mint, (accuracy - 70f) / 30f);
        }

        internal static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static float GammaToLinear(this float v) => Mathf.GammaToLinearSpace(v);
        private static float LinearToGamma(this float v) => Mathf.LinearToGammaSpace(Mathf.Clamp01(v));
    }
}
