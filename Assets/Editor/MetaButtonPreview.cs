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
                Debug.Log($"[MetaButtonPreview] wrote Logs/meta-button-*.png, lighting '{glow.BoneName}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MetaButtonPreview] failed: {e}");
                exit = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(exit);
        }

        private static void Look(Camera cam, Vector3 from)
        {
            var target = new Vector3(0.005f, -0.005f, 0f);
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(target - from, Vector3.up);
        }
    }
}
