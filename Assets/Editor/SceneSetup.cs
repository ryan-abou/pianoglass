using PianoGlass.Highway;
using PianoGlass.Piano;
using PianoGlass.Scoring;
using PianoGlass.Song;
using PianoGlass.Transport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Wires the falling-note vertical slice into SampleScene: a SongClock, a
    /// NoteHighway, and a SongTransport (+OvrSongTransport) for play/pause/
    /// restart, all pointed at whatever KeyboardCalibration is already there.
    /// Also points CalibrationPanel at the transport and clock so it can show
    /// transport state, and adds/wires a CanvasCalibrationFollower on the
    /// status Canvas so it registers on the keyboard instead of sitting at
    /// its authored world position. Idempotent -- run it again after changing
    /// a script and it updates the existing objects instead of duplicating them.
    ///
    /// Doing this by hand in the Inspector is exactly the kind of setup that
    /// silently drifts between machines and gets forgotten right before a
    /// build.
    /// </summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string NoteMaterialPath = "Assets/NoteHighway.mat";
        private const string TranslucentMaterialPath = "Assets/KeyOverlayTranslucent.mat";
        private const string CameraRigName = "[BuildingBlock] Camera Rig";

        [MenuItem("PianoGlass/Scene/Add Falling-Note Highway")]
        public static void AddFallingNoteHighway()
        {
            Scene scene = OpenScene();

            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            if (calibration == null)
            {
                Debug.LogError("[SceneSetup] No KeyboardCalibration in the scene -- " +
                                "set that up first. NoteHighway has nothing to position " +
                                "notes against without it.");
                return;
            }

            SongClock clock = GetOrAdd<SongClock>("SongClock");
            NoteHighway highway = GetOrAdd<NoteHighway>("NoteHighway");
            SongTransport transport = GetOrAdd<SongTransport>("SongTransport");

            // OvrSongTransport requires SongTransport on the same object, so
            // it always ends up paired the way KeyboardCalibration and
            // OvrCalibrationInput are.
            if (transport.GetComponent<OvrSongTransport>() == null)
                transport.gameObject.AddComponent<OvrSongTransport>();

            Wire(highway, calibration, clock);
            WireTransport(transport, calibration, clock);

            // playOnStart defaults to false in code now, but a SongClock
            // already saved into the scene from before that change would
            // still have the old serialized value -- set it explicitly so
            // this menu item stays the single source of truth regardless.
            SetPlayOnStart(clock, false);

            CalibrationPanel panel = Object.FindAnyObjectByType<CalibrationPanel>();
            if (panel != null) WireCalibrationPanel(panel, transport, clock);

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                CanvasCalibrationFollower follower = canvas.GetComponent<CanvasCalibrationFollower>();
                if (follower == null) follower = canvas.gameObject.AddComponent<CanvasCalibrationFollower>();
                WireCanvasFollower(follower, calibration);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] SongClock + NoteHighway + SongTransport wired into " +
                      $"{scene.name}, CalibrationPanel and the status Canvas updated, and saved.");
        }

        /// <summary>
        /// Turns passthrough on: OVRManager's Enable Passthrough flag, an
        /// Underlay OVRPassthroughLayer on the camera rig, the main camera
        /// cleared to a fully transparent solid colour instead of the
        /// Skybox, and the scene's skybox cleared to None.
        ///
        /// Also raises the project's Passthrough Support feature flag
        /// (OVRProjectConfig) to Required -- that is the setting Meta's
        /// build step reads to decide whether the generated AndroidManifest
        /// gets the passthrough uses-feature entries. It lives in a
        /// separate asset from the scene, which is why this is set here
        /// rather than left to be inferred from the OVRManager component.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Passthrough")]
        public static void ConfigurePassthrough()
        {
            Scene scene = OpenScene();

            GameObject cameraRig = GameObject.Find(CameraRigName);
            if (cameraRig == null)
            {
                Debug.LogError($"[SceneSetup] No '{CameraRigName}' in the scene -- add the Meta " +
                                "Building Block camera rig first. OVRManager has nowhere to live without it.");
                return;
            }

            OVRManager manager = cameraRig.GetComponent<OVRManager>();
            if (manager == null)
            {
                Debug.LogError($"[SceneSetup] '{CameraRigName}' has no OVRManager -- expected the " +
                                "Building Block to have added one already.");
                return;
            }

            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("isInsightPassthroughEnabled").boolValue = true;
            managerSo.ApplyModifiedProperties();

            // overlayType used to need setting explicitly to Underlay, but as
            // of Meta XR SDK 205.0.0 it's [Obsolete] and [HideInInspector]:
            // flexible layering is gone and there is only ever one background
            // passthrough layer now, rendered beneath the eye buffer -- which
            // is exactly Underlay, so there is nothing left to set here.
            OVRPassthroughLayer layer = cameraRig.GetComponent<OVRPassthroughLayer>();
            if (layer == null) layer = cameraRig.AddComponent<OVRPassthroughLayer>();
            layer.textureOpacity = 1f;

            Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[SceneSetup] No camera found -- cannot set clear flags/background.");
                return;
            }

            // Skybox clear flags render an opaque sky over everything,
            // passthrough included -- a camera that is supposed to show
            // passthrough must clear to a colour, and that colour's alpha
            // must be 0 so the compositor's passthrough layer shows through
            // instead of whatever this colour would otherwise be.
            cam.clearFlags = CameraClearFlags.SolidColor;
            Color background = cam.backgroundColor;
            background.a = 0f;
            cam.backgroundColor = background;

            // A Skybox clear mode draws this material even if nothing else
            // references it, so it has to go too, not just the clear flags.
            RenderSettings.skybox = null;

            OVRProjectConfig config = OVRProjectConfig.CachedProjectConfig;
            if (config != null)
            {
                config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
                OVRProjectConfig.CommitProjectConfig(config);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning("[SceneSetup] OVRProjectConfig not available -- Passthrough Support " +
                                 "feature flag left unchanged. Set it to Required by hand under " +
                                 "Meta > Tools > Project Setup Tool, or re-run this once the editor " +
                                 "has finished loading.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SceneSetup] Passthrough configured: OVRManager.isInsightPassthroughEnabled=true, " +
                      "OVRPassthroughLayer (Underlay) on the camera rig, camera SolidColor/alpha 0, " +
                      "skybox cleared, project Passthrough Support = Required.");
        }

        /// <summary>
        /// Wires PianoKeyHighlighter to the SongClock its ActiveKeysOnly/
        /// EdgeMarkers modes need, creates and assigns the Transparent-
        /// surface material Translucent mode needs, and adds
        /// OvrKeyOverlayInput (left X cycles the mode, left stick click
        /// toggles passthrough) pointed at both that highlighter and
        /// whatever OVRPassthroughLayer Configure Passthrough added.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Key Overlay")]
        public static void ConfigureKeyOverlay()
        {
            Scene scene = OpenScene();

            var highlighter = Object.FindAnyObjectByType<PianoKeyHighlighter>();
            if (highlighter == null)
            {
                Debug.LogError("[SceneSetup] No PianoKeyHighlighter in the scene -- set up key " +
                                "highlighting first.");
                return;
            }

            SongClock clock = GetOrAdd<SongClock>("SongClock");

            var highlighterSo = new SerializedObject(highlighter);
            highlighterSo.FindProperty("clock").objectReferenceValue = clock;

            SerializedProperty materialProp = highlighterSo.FindProperty("translucentMaterial");
            if (materialProp.objectReferenceValue == null)
                materialProp.objectReferenceValue = GetOrCreateTranslucentMaterial();

            highlighterSo.ApplyModifiedProperties();

            GameObject host = highlighter.gameObject;
            OvrKeyOverlayInput overlayInput = host.GetComponent<OvrKeyOverlayInput>();
            if (overlayInput == null) overlayInput = host.AddComponent<OvrKeyOverlayInput>();

            GameObject cameraRig = GameObject.Find(CameraRigName);
            OVRPassthroughLayer layer = cameraRig != null ? cameraRig.GetComponent<OVRPassthroughLayer>() : null;
            if (layer == null)
                Debug.LogWarning("[SceneSetup] No OVRPassthroughLayer found -- run " +
                                 "PianoGlass/Scene/Configure Passthrough first so the stick-click " +
                                 "toggle has something to toggle.");

            var overlayInputSo = new SerializedObject(overlayInput);
            overlayInputSo.FindProperty("highlighter").objectReferenceValue = highlighter;
            overlayInputSo.FindProperty("passthroughLayer").objectReferenceValue = layer;
            overlayInputSo.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SceneSetup] Key overlay configured: PianoKeyHighlighter wired to SongClock " +
                      "and a translucent material, OvrKeyOverlayInput added for the left X / stick-click controls.");
        }

        /// <summary>
        /// Adds ControllerTipCalibration next to KeyboardCalibration (left
        /// menu button measures where the controller tip really is) and
        /// points it, and CalibrationPanel, at the right objects. Yaw
        /// fine-adjust needs no wiring -- it lives in KeyboardCalibration and
        /// OvrCalibrationInput already. Idempotent.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Calibration Tools")]
        public static void ConfigureCalibrationTools()
        {
            Scene scene = OpenScene();

            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            if (calibration == null)
            {
                Debug.LogError("[SceneSetup] No KeyboardCalibration in the scene -- nothing to attach tip calibration to.");
                return;
            }

            var calibrationSo = new SerializedObject(calibration);
            var pointerTip = calibrationSo.FindProperty("pointerTip").objectReferenceValue as Transform;
            if (pointerTip == null || pointerTip.parent == null)
                Debug.LogWarning("[SceneSetup] KeyboardCalibration.pointerTip is unassigned or has no parent -- " +
                                 "tip calibration samples the parent (controller) pose and will refuse to run.");

            GameObject host = calibration.gameObject;
            var tip = host.GetComponent<ControllerTipCalibration>();
            if (tip == null) tip = host.AddComponent<ControllerTipCalibration>();

            var tipSo = new SerializedObject(tip);
            tipSo.FindProperty("pointerTip").objectReferenceValue = pointerTip;
            tipSo.ApplyModifiedProperties();

            CalibrationPanel panel = Object.FindAnyObjectByType<CalibrationPanel>();
            if (panel != null)
            {
                var panelSo = new SerializedObject(panel);
                panelSo.FindProperty("tip").objectReferenceValue = tip;
                panelSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SceneSetup] Calibration tools configured: ControllerTipCalibration on " +
                      $"'{host.name}' (pointerTip = {(pointerTip != null ? pointerTip.name : "none")}), " +
                      $"CalibrationPanel {(panel != null ? "wired" : "not found")}.");
        }

        /// <summary>
        /// Adds the development-only spatial-anchor test harness
        /// (OvrAnchorSpike: right grip create+save, left grip load, both grips
        /// held erase) next to KeyboardCalibration, wired to it, to the
        /// ControllerTipCalibration (so it stays quiet while the tip is
        /// measured) and to NoteHighway.mat for its markers. The component
        /// disables itself in release builds. Idempotent.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Anchor Spike")]
        public static void ConfigureAnchorSpike()
        {
            Scene scene = OpenScene();

            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            if (calibration == null)
            {
                Debug.LogError("[SceneSetup] No KeyboardCalibration in the scene -- the anchor spike needs a keyboard to anchor.");
                return;
            }

            GameObject host = calibration.gameObject;
            var spike = host.GetComponent<OvrAnchorSpike>();
            if (spike == null) spike = host.AddComponent<OvrAnchorSpike>();

            var so = new SerializedObject(spike);
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.FindProperty("tip").objectReferenceValue = host.GetComponent<ControllerTipCalibration>();
            SerializedProperty materialProp = so.FindProperty("markerMaterial");
            if (materialProp.objectReferenceValue == null)
                materialProp.objectReferenceValue = GetOrCreateNoteMaterial();
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] Anchor spike configured on '{host.name}' " +
                      $"(tip {(host.GetComponent<ControllerTipCalibration>() != null ? "wired" : "missing")}, " +
                      $"marker material {(materialProp.objectReferenceValue != null ? materialProp.objectReferenceValue.name : "none")}).");
        }

        /// <summary>
        /// Phase 4: the real overlays follow a spatial anchor. Adds
        /// PianoAnchorManager and CalibrationFlowController next to
        /// KeyboardCalibration (useAnchoredCalibration on -- it only takes
        /// effect in development builds), points CalibrationPanel at the
        /// controller so anchor status shows in the headset, and disables
        /// OvrAnchorSpike: its grip bindings would clash with the Phase 5
        /// corrections, and two things creating anchors from one keyboard is
        /// one too many. The spike's code and component stay, disabled.
        /// Idempotent.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Anchored Calibration")]
        public static void ConfigureAnchoredCalibration()
        {
            Scene scene = OpenScene();

            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            if (calibration == null)
            {
                Debug.LogError("[SceneSetup] No KeyboardCalibration in the scene -- nothing to anchor.");
                return;
            }

            GameObject host = calibration.gameObject;
            var manager = host.GetComponent<PianoAnchorManager>();
            if (manager == null) manager = host.AddComponent<PianoAnchorManager>();

            var controller = host.GetComponent<CalibrationFlowController>();
            if (controller == null) controller = host.AddComponent<CalibrationFlowController>();

            var so = new SerializedObject(controller);
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.FindProperty("anchorManager").objectReferenceValue = manager;
            so.FindProperty("useAnchoredCalibration").boolValue = true;
            so.ApplyModifiedProperties();

            CalibrationPanel panel = Object.FindAnyObjectByType<CalibrationPanel>();
            if (panel != null)
            {
                var panelSo = new SerializedObject(panel);
                panelSo.FindProperty("flow").objectReferenceValue = controller;
                panelSo.ApplyModifiedProperties();
            }

            var spike = host.GetComponent<OvrAnchorSpike>();
            if (spike != null)
            {
                spike.enabled = false;
                EditorUtility.SetDirty(spike);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] Anchored calibration configured on '{host.name}': PianoAnchorManager + " +
                      $"CalibrationFlowController, CalibrationPanel {(panel != null ? "wired" : "not found")}, " +
                      $"OvrAnchorSpike {(spike != null ? "disabled" : "not present")}.");
        }

        /// <summary>
        /// Removes the leftover default "Cube" GameObject from Day 1 testing
        /// -- a plain 1x1x1 primitive sitting at the scene root, unrelated to
        /// any of the piano/overlay/highway wiring above. Idempotent: logs
        /// and does nothing if it's already gone.
        /// </summary>
        /// <summary>
        /// Adds ScoreKeeper next to SongTransport, wired to the clock,
        /// transport and KeyboardCalibration, and points CalibrationPanel at
        /// it for the score line. Idempotent.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Scoring")]
        public static void ConfigureScoring()
        {
            Scene scene = OpenScene();

            var transport = Object.FindAnyObjectByType<SongTransport>();
            var clock = Object.FindAnyObjectByType<SongClock>();
            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            if (transport == null || clock == null || calibration == null)
            {
                Debug.LogError("[SceneSetup] SongTransport, SongClock or KeyboardCalibration missing -- " +
                               "run Add Falling-Note Highway first.");
                return;
            }

            GameObject host = transport.gameObject;
            var keeper = host.GetComponent<ScoreKeeper>();
            if (keeper == null) keeper = host.AddComponent<ScoreKeeper>();

            var so = new SerializedObject(keeper);
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("transport").objectReferenceValue = transport;
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.ApplyModifiedProperties();

            CalibrationPanel panel = Object.FindAnyObjectByType<CalibrationPanel>();
            if (panel != null)
            {
                var panelSo = new SerializedObject(panel);
                panelSo.FindProperty("score").objectReferenceValue = keeper;
                panelSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] Scoring configured: ScoreKeeper on '{host.name}', " +
                      $"CalibrationPanel {(panel != null ? "wired" : "not found")}.");
        }

        [MenuItem("PianoGlass/Scene/Configure Display Latency")]
        public static void ConfigureDisplayLatency()
        {
            Scene scene = OpenScene();

            var clock = Object.FindAnyObjectByType<SongClock>();
            if (clock == null)
            {
                Debug.LogError("[SceneSetup] SongClock missing -- run Add Falling-Note Highway first.");
                return;
            }

            if (clock.GetComponent<OvrDisplayLatency>() == null) clock.gameObject.AddComponent<OvrDisplayLatency>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] Display latency configured: OvrDisplayLatency on '{clock.gameObject.name}'.");
        }

        [MenuItem("PianoGlass/Scene/Remove Test Cube")]
        public static void RemoveTestCube()
        {
            Scene scene = OpenScene();

            GameObject cube = GameObject.Find("Cube");
            if (cube == null)
            {
                Debug.Log("[SceneSetup] No 'Cube' GameObject in the scene -- nothing to remove.");
                return;
            }

            // Belt and suspenders: confirm it's the plain Day 1 test primitive
            // (a default Cube mesh, no scripts) before deleting anything,
            // rather than trusting the name alone.
            bool looksLikeTestCube = cube.GetComponent<MeshFilter>() != null
                                     && cube.GetComponents<MonoBehaviour>().Length == 0;
            if (!looksLikeTestCube)
            {
                Debug.LogWarning("[SceneSetup] Found a 'Cube' GameObject, but it has scripts on it " +
                                 "or no MeshFilter -- that doesn't look like the Day 1 test primitive. " +
                                 "Leaving it alone; remove it by hand if you're sure.");
                return;
            }

            Object.DestroyImmediate(cube);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[SceneSetup] Removed the leftover Day 1 test 'Cube'.");
        }

        // ------------------------------------------------------------------

        private static Scene OpenScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return scene;
        }

        private static T GetOrAdd<T>(string objectName) where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>();
            if (existing != null) return existing;

            var go = new GameObject(objectName);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// Assigns NoteHighway's private serialized fields directly.
        /// SerializedObject rather than a public setter API, because these
        /// references only ever need setting from editor tooling like this --
        /// adding runtime setters just for this would be surface area nothing
        /// else uses.
        /// </summary>
        private static void Wire(NoteHighway highway, KeyboardCalibration calibration, SongClock clock)
        {
            var so = new SerializedObject(highway);
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("depthOffset").floatValue = HighwayDepthOffset;

            SerializedProperty materialProp = so.FindProperty("noteMaterial");
            if (materialProp.objectReferenceValue == null)
                materialProp.objectReferenceValue = GetOrCreateNoteMaterial();

            so.ApplyModifiedProperties();
        }

        private static void WireTransport(SongTransport transport, KeyboardCalibration calibration, SongClock clock)
        {
            var so = new SerializedObject(transport);
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.FindProperty("clock").objectReferenceValue = clock;
            so.ApplyModifiedProperties();
            // chordNoteA / chordNoteB are left alone -- their defaults (21,
            // 22) are correct and a hand-tuned override shouldn't be clobbered.
        }

        private static void SetPlayOnStart(SongClock clock, bool value)
        {
            var so = new SerializedObject(clock);
            so.FindProperty("playOnStart").boolValue = value;
            so.ApplyModifiedProperties();
        }

        private static void WireCalibrationPanel(CalibrationPanel panel, SongTransport transport, SongClock clock)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("transport").objectReferenceValue = transport;
            so.FindProperty("clock").objectReferenceValue = clock;
            so.ApplyModifiedProperties();
        }

        /// <summary>The note wall and the status panel used to share one
        /// standoff (NoteHighway.depthOffset, 0.05 in the scene). The wall now
        /// sits HighwayDepthOffset behind the keys -- 40 mm closer to the
        /// player -- while the panel keeps PanelDepthOffset, exactly where it
        /// was. Both set here so the scene cannot keep a stale override.</summary>
        private const float HighwayDepthOffset = 0.010f;
        private const float PanelDepthOffset = 0.05f;

        private static void WireCanvasFollower(CanvasCalibrationFollower follower, KeyboardCalibration calibration)
        {
            var so = new SerializedObject(follower);
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.FindProperty("depthOffset").floatValue = PanelDepthOffset;
            so.ApplyModifiedProperties();
        }

        private static Material GetOrCreateNoteMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(NoteMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[SceneSetup] URP Unlit shader not found -- leaving " +
                                 "NoteHighway's material unassigned. Assign one by hand.");
                return null;
            }

            var mat = new Material(shader) { color = new Color(0.40f, 0.68f, 0.95f, 1f) };
            AssetDatabase.CreateAsset(mat, NoteMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneSetup] created {NoteMaterialPath}");
            return mat;
        }

        /// <summary>
        /// A URP Unlit material forced into the Transparent surface type, so
        /// the per-instance alpha PianoKeyHighlighter writes via
        /// MaterialPropertyBlock in Translucent mode is actually honoured.
        /// An Opaque surface (what `new Material(shader)` defaults to)
        /// ignores alpha entirely -- silently, with nothing in the console
        /// to explain why Translucent looks identical to FullOverlay. This
        /// replicates by hand what toggling Surface Type to Transparent in
        /// the inspector does (see URP's BaseShaderGUI/UnlitShader), since
        /// that editor-only GUI code isn't something a build script can call.
        /// </summary>
        private static Material GetOrCreateTranslucentMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(TranslucentMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[SceneSetup] URP Unlit shader not found -- leaving the " +
                                 "translucent material unassigned. Assign one by hand.");
                return null;
            }

            var mat = new Material(shader) { name = "KeyOverlayTranslucent" };

            mat.SetFloat("_Surface", 1f);  // SurfaceType.Transparent
            mat.SetFloat("_Blend", 0f);    // BlendMode.Alpha
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHAMODULATE_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.color = new Color(1f, 1f, 1f, 0.25f);

            AssetDatabase.CreateAsset(mat, TranslucentMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneSetup] created {TranslucentMaterialPath}");
            return mat;
        }
    }
}
