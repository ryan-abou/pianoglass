using System;
using System.IO;
using PianoGlass.Piano;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Renders the Meta button highlight (#42) to PNGs without a headset, for
    /// Ryan to approve first: the Quest 3 right controller model with
    /// ControllerButtonGlow on it, seen from where your eyes are when you
    /// hold the controller in front of you, at the pulse's brightest and
    /// dimmest. Composited over grey the way GlassPreview does.
    ///
    ///   Unity.exe -batchmode -quit -projectPath ... -buildTarget Android
    ///     -executeMethod PianoGlass.EditorTools.MetaButtonPreview.Render
    ///
    /// Output: Logs/meta-button-bright.png, Logs/meta-button-dim.png,
    /// Logs/meta-button-side.png. Not -nographics: it needs a GPU.
    ///
    /// Round 15: RenderDiagram draws the controller square-on to the Meta
    /// button with the button lit, on a see-through background, to
    /// Assets/UI/MetaButtonDiagram.png (the recenter banner's picture), then
    /// the banner itself, as it ships, to Logs/meta-button-banner.png. Run
    /// SceneSetup.ConfigureMetaButtonHint after, to wire the picture in.
    /// </summary>
    public static class MetaButtonPreview
    {
        private const string ModelPath = "Packages/com.meta.xr.sdk.core/Meshes/MetaQuestTouchPlus/MetaQuestTouchPlus_Right.fbx";
        private const string MaterialPath = "Assets/KeyOverlayTranslucent.mat";
        private const string CapPath = "Assets/Meshes/MetaButtonCap_TouchPlusRight.asset";

        [MenuItem("PianoGlass/Preview/Render Meta Button Preview")]
        public static void Render()
        {
            int exit = 0;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var model = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                var cap = AssetDatabase.LoadAssetAtPath<Mesh>(CapPath);

                var camGo = new GameObject("PreviewCamera") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.fieldOfView = 28f;
                cam.nearClipPlane = 0.01f;
                cam.allowHDR = false;

                ControllerButtonGlow glow = ControllerButtonGlow.Add(model, material, cap, ControllerButtonGlow.MetaButtonBones);
                if (glow == null) throw new Exception("no Meta button bone on the model");

                // Held in front of you, pointing away: the eyes are above and behind.
                Look(cam, new Vector3(0.02f, 0.22f, -0.24f));
                // sin(2 pi 1.1 t) = 1 at t = 0.227: brightest; -1 at 0.682: dimmest.
                glow.Pulse(0.227f);
                GlassPreview.CaptureOver(cam, null, "Logs/meta-button-bright.png");
                glow.Pulse(0.682f);
                GlassPreview.CaptureOver(cam, null, "Logs/meta-button-dim.png");
                // From the right side, to see the button sits on the face.
                Look(cam, new Vector3(0.22f, 0.12f, -0.12f));
                glow.Pulse(0.227f);
                GlassPreview.CaptureOver(cam, null, "Logs/meta-button-side.png");
                // Turned over, from underneath: the glow should be gone (round 15).
                Look(cam, new Vector3(0.02f, -0.22f, -0.12f));
                glow.Pulse(0.227f);
                GlassPreview.CaptureOver(cam, null, "Logs/meta-button-behind.png");
                Debug.Log($"[MetaButtonPreview] wrote Logs/meta-button-*.png, lighting '{glow.BoneName}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaButtonPreview] failed: {e}");
                exit = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        private const string DiagramPath = "Assets/UI/MetaButtonDiagram.png";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const int DiagramPixels = 512;
        /// <summary>A layer nothing in the scene uses, so the banner preview sees only the banner.</summary>
        private const int PreviewLayer = 31;

        [MenuItem("PianoGlass/Preview/Render Meta Button Diagram")]
        public static void RenderDiagram()
        {
            int exit = 0;
            try
            {
                Diagram();
                Banner();
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaButtonPreview] diagram failed: {e}");
                exit = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        /// <summary>The controller seen square-on to the Meta button, tip up,
        /// drawn with its own materials, the button glowing at its brightest.</summary>
        private static void Diagram()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var model = (GameObject)UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var cap = AssetDatabase.LoadAssetAtPath<Mesh>(CapPath);
            if (cap == null) throw new Exception($"no {CapPath}: run SceneSetup.ConfigureMetaButtonHint first");

            ControllerButtonGlow glow = ControllerButtonGlow.Add(model, material, cap, ControllerButtonGlow.MetaButtonBones);
            if (glow == null) throw new Exception("no Meta button bone on the model");
            // The glow hides the model on the headset; in the picture it is the point.
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                if (!r.name.StartsWith("MetaButton")) r.enabled = true;

            SkinnedMeshRenderer body = null;
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != cap) { body = smr; break; }
            if (body == null) throw new Exception("no controller mesh");
            (Vector3 n, float _) = ControllerButtonGlow.Facing(cap, cap.bounds.center);
            Vector3 up = body.transform.TransformDirection(n).normalized;
            Bounds all = body.bounds;

            var lightGo = new GameObject("Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.LookRotation(-up + Vector3.forward * 0.3f);
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);

            var camGo = new GameObject("DiagramCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = true;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 2f;
            cam.allowHDR = false;
            // Square-on to the button, centred on the controller, its tip at the top.
            Vector3 tip = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
            camGo.transform.SetPositionAndRotation(all.center + up * 0.5f, Quaternion.LookRotation(-up, tip));
            cam.orthographicSize = all.extents.magnitude * 0.8f;
            glow.Pulse(0.227f);

            var rt = new RenderTexture(DiagramPixels, DiagramPixels, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                antiAliasing = 8
            };
            cam.targetTexture = rt;
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, request))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(DiagramPixels, DiagramPixels, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, DiagramPixels, DiagramPixels), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            rt.Release();

            File.WriteAllBytes(DiagramPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(DiagramPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(DiagramPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            File.Copy(DiagramPath, "Logs/meta-button-diagram.png", true);
            Debug.Log($"[MetaButtonPreview] wrote {DiagramPath}");
        }

        /// <summary>The recenter banner as LaunchResync builds it, with the
        /// picture, over grey (passthrough stands in).</summary>
        private static void Banner()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var picker = UnityEngine.Object.FindAnyObjectByType<PianoGlass.UI.SongPicker>(FindObjectsInactive.Include);
            if (picker == null) throw new Exception("no song picker in the scene");
            RecenterPromptView view = RecenterPromptView.Create(picker, LaunchResync.PromptLine);
            view.AddPicture(AssetDatabase.LoadAssetAtPath<Texture2D>(DiagramPath), LaunchResync.PictureMm);
            view.gameObject.SetActive(true);
            view.enabled = false; // placed here, not after a head

            var far = new Vector3(0f, 200f, 0f);
            view.transform.SetPositionAndRotation(far + Vector3.forward * 0.9f, Quaternion.identity);
            foreach (Transform t in view.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PreviewLayer;
            foreach (PianoGlass.UI.GlassPanel g in view.GetComponentsInChildren<PianoGlass.UI.GlassPanel>(true)) g.RebuildNow();
            Canvas.ForceUpdateCanvases();
            foreach (TMPro.TMP_Text t in view.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.ForceMeshUpdate();

            var camGo = new GameObject("BannerCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << PreviewLayer;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            cam.allowHDR = false;
            camGo.transform.SetPositionAndRotation(far, Quaternion.identity);
            GlassPreview.CaptureOver(cam, null, "Logs/meta-button-banner.png");
            Debug.Log("[MetaButtonPreview] wrote Logs/meta-button-banner.png");
        }

        private static void Look(Camera cam, Vector3 from)
        {
            var target = new Vector3(0.005f, -0.005f, 0f);
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(target - from, Vector3.up);
        }
    }
}
