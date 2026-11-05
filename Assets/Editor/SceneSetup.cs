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

            Canvas canvas = StatusCanvas();
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
        /// Sets KeyboardCalibration's white key depth to the measured value
        /// (PianoKeyLayout.MeasuredWhiteKeyDepth). Black key inset and length
        /// are constants in PianoKeyLayout and need no scene wiring.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Key Dimensions")]
        public static void ConfigureKeyDimensions()
        {
            Scene scene = OpenScene();

            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            if (calibration == null)
            {
                Debug.LogError("[SceneSetup] No KeyboardCalibration in the scene.");
                return;
            }

            var so = new SerializedObject(calibration);
            so.FindProperty("whiteKeyDepth").floatValue = PianoKeyLayout.MeasuredWhiteKeyDepth;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SceneSetup] White key depth set to {PianoKeyLayout.MeasuredWhiteKeyDepth * 1000f:0.00} mm.");
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

            // Outcome colours (F19/F20): slabs tint on hit/miss, keys pulse
            // coral on a wrong press. Both only listen to ScoreKeeper.
            NoteHighway highway = Object.FindAnyObjectByType<NoteHighway>();
            if (highway != null)
            {
                var highwaySo = new SerializedObject(highway);
                highwaySo.FindProperty("scoreKeeper").objectReferenceValue = keeper;
                highwaySo.ApplyModifiedProperties();
            }
            PianoKeyHighlighter highlighter = Object.FindAnyObjectByType<PianoKeyHighlighter>();
            if (highlighter != null)
            {
                var highlighterSo = new SerializedObject(highlighter);
                highlighterSo.FindProperty("scoreKeeper").objectReferenceValue = keeper;
                highlighterSo.ApplyModifiedProperties();
            }
            if (highway == null || highlighter == null)
                Debug.LogWarning("[SceneSetup] NoteHighway or PianoKeyHighlighter missing -- outcome colours not wired.");

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

        /// <summary>
        /// The highway grid (Phase 1, F16/F17): a HighwayGrid next to
        /// NoteHighway with its material, and the highway leaned back into
        /// the reference video's runway. Tilt is set here rather than left
        /// in the scene so the angle under review is the one in this file.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Highway Grid")]
        public static void ConfigureHighwayGrid()
        {
            Scene scene = OpenScene();

            var highway = Object.FindAnyObjectByType<NoteHighway>();
            if (highway == null)
            {
                Debug.LogError("[SceneSetup] NoteHighway missing -- run Add Falling-Note Highway first.");
                return;
            }

            // Re-created, not reused, so the look under review is always
            // HighwayGrid's code defaults -- a scene copy of last week's
            // values would silently override them.
            HighwayGrid old = highway.GetComponent<HighwayGrid>();
            if (old != null) Object.DestroyImmediate(old);
            HighwayGrid grid = highway.gameObject.AddComponent<HighwayGrid>();

            var gridSo = new SerializedObject(grid);
            gridSo.FindProperty("highway").objectReferenceValue = highway;
            gridSo.FindProperty("gridMaterial").objectReferenceValue = GetOrCreateGridMaterial();
            gridSo.ApplyModifiedProperties();

            var highwaySo = new SerializedObject(highway);
            highwaySo.FindProperty("tiltDegrees").floatValue = HighwayTiltDegrees;
            highwaySo.FindProperty("depthOffset").floatValue = HighwayDepthOffset;
            highwaySo.ApplyModifiedProperties();

            // The panel keeps its original spot even though the leaned-back
            // runway now runs under it: it is a development readout that the
            // menus replace, not part of the finished look (Ryan, 2026-11-02).
            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            Canvas canvas = StatusCanvas();
            CanvasCalibrationFollower follower = canvas != null ? canvas.GetComponent<CanvasCalibrationFollower>() : null;
            if (follower != null && calibration != null) WireCanvasFollower(follower, calibration);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] Highway grid configured on '{highway.gameObject.name}', " +
                      $"tilt {HighwayTiltDegrees} deg.");
        }

        /// <summary>Degrees the highway leans back from upright. The video's
        /// runway rises roughly 25-35 degrees above horizontal behind the keys;
        /// tuned against headset screenshots in the grid spike.</summary>
        private const float HighwayTiltDegrees = -60f;

        private const string GridShaderPath = "Assets/Shaders/HighwayGrid.shader";
        private const string GridMaterialPath = "Assets/HighwayGrid.mat";

        private static Material GetOrCreateGridMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath);
            if (existing != null) return existing;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GridShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[SceneSetup] {GridShaderPath} not found -- HighwayGrid left without a material.");
                return null;
            }

            var mat = new Material(shader) { name = "HighwayGrid" };
            AssetDatabase.CreateAsset(mat, GridMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneSetup] created {GridMaterialPath}");
            return mat;
        }

        private const string LabelFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        /// <summary>
        /// Note names on the slabs (F14): gives NoteHighway its label font and
        /// the font's own SDF material. Assigned here rather than looked up at
        /// runtime -- a runtime lookup is the Shader.Find trap again. The
        /// static LiberationSans atlas has no U+266F, but its fallback table
        /// points at the dynamic LiberationSans atlas, whose TTF does, so the
        /// real sharp sign draws; NoteHighway checks and falls back to '#'.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Note Names")]
        public static void ConfigureNoteNames()
        {
            Scene scene = OpenScene();

            var highway = Object.FindAnyObjectByType<NoteHighway>();
            if (highway == null)
            {
                Debug.LogError("[SceneSetup] NoteHighway missing -- run Add Falling-Note Highway first.");
                return;
            }

            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(LabelFontPath);
            if (font == null)
            {
                Debug.LogError($"[SceneSetup] {LabelFontPath} not found -- import TMP Essential Resources.");
                return;
            }

            var so = new SerializedObject(highway);
            so.FindProperty("showNoteNames").boolValue = true;
            so.FindProperty("labelFont").objectReferenceValue = font;
            so.FindProperty("labelMaterial").objectReferenceValue = font.material;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] Note names configured: font '{font.name}', material '{font.material.name}'.");
        }

        private const string GlassShaderPath = "Assets/Shaders/LiquidGlass.shader";
        private const string GlassMaterialPath = "Assets/LiquidGlass.mat";
        private const string UiFolder = "Assets/UI";
        private const string GlassTokensPath = "Assets/UI/GlassTokens.asset";

        /// <summary>Outfit's built-in line height is 1.26 em against
        /// LiberationSans's 1.15; this keeps the status panel's many lines
        /// the height they were laid out for (TMP units: 1/100 em).</summary>
        private const float GlassLineSpacing = -11f;

        /// <summary>
        /// Liquid Glass (Batch B1): the shared glass material and token
        /// asset, created if missing, and a GlassPanel behind the status
        /// panel's Canvas -- the first surface to try it on. The panel's text
        /// takes the primary text colour from the tokens. The GlassPanel is
        /// re-created so its code defaults (padding, depth) are the ones
        /// under review, as Configure Highway Grid does.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Liquid Glass")]
        public static void ConfigureLiquidGlass()
        {
            Scene scene = OpenScene();

            Canvas canvas = StatusCanvas();
            if (canvas == null)
            {
                Debug.LogError("[SceneSetup] No Canvas holding the CalibrationPanel -- nothing to put glass behind.");
                return;
            }

            Material material = GetOrCreateGlassMaterial();
            PianoGlass.UI.GlassTokens tokens = GetOrCreateGlassTokens(resetToCodeDefaults: true);
            if (material == null || tokens == null) return;

            AddGlass(canvas.gameObject, material, tokens);
            TMPro.TMP_FontAsset font = GlassFont.GetOrCreate();
            Material textMaterial = GlassTextMaterial.GetOrCreate(font);

            foreach (TMPro.TMP_Text t in canvas.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                Undo.RecordObject(t, "Glass text");
                t.color = tokens.textPrimary;
                if (font != null) t.font = font;
                if (textMaterial != null) t.fontSharedMaterial = textMaterial;
                t.lineSpacing = GlassLineSpacing;
                EditorUtility.SetDirty(t);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SceneSetup] Liquid Glass on '{canvas.name}': {GlassMaterialPath}, {GlassTokensPath}.");
        }

        private static PianoGlass.UI.GlassPanel AddGlass(GameObject target, Material material, PianoGlass.UI.GlassTokens tokens)
        {
            var old = target.GetComponent<PianoGlass.UI.GlassPanel>();
            if (old != null) Object.DestroyImmediate(old);
            var glass = target.AddComponent<PianoGlass.UI.GlassPanel>();
            var so = new SerializedObject(glass);
            so.FindProperty("glassMaterial").objectReferenceValue = material;
            so.FindProperty("tokens").objectReferenceValue = tokens;
            so.ApplyModifiedProperties();
            return glass;
        }

        private static Material GetOrCreateGlassMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
            if (existing != null) return existing;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GlassShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[SceneSetup] {GlassShaderPath} not found -- no glass material.");
                return null;
            }
            var mat = new Material(shader) { name = "LiquidGlass" };
            AssetDatabase.CreateAsset(mat, GlassMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneSetup] created {GlassMaterialPath}");
            return mat;
        }

        /// <param name="resetToCodeDefaults">Overwrite the asset with
        /// GlassTokens' code defaults, so the look under review is the one in
        /// the code (as Configure Highway Grid does), not values saved by an
        /// earlier version. Only Configure Liquid Glass passes true.</param>
        private static PianoGlass.UI.GlassTokens GetOrCreateGlassTokens(bool resetToCodeDefaults = false)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PianoGlass.UI.GlassTokens>(GlassTokensPath);
            if (existing != null)
            {
                if (resetToCodeDefaults)
                {
                    var fresh = ScriptableObject.CreateInstance<PianoGlass.UI.GlassTokens>();
                    EditorUtility.CopySerialized(fresh, existing);
                    existing.name = "GlassTokens";   // CopySerialized copies the empty name too
                    Object.DestroyImmediate(fresh);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[SceneSetup] {GlassTokensPath} reset to code defaults");
                }
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(UiFolder)) AssetDatabase.CreateFolder("Assets", "UI");
            var tokens = ScriptableObject.CreateInstance<PianoGlass.UI.GlassTokens>();
            AssetDatabase.CreateAsset(tokens, GlassTokensPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SceneSetup] created {GlassTokensPath}");
            return tokens;
        }

        /// <summary>The card sits low and close: its centre this far above the
        /// keys, at the foot of the leaned-back wall, in front of the status
        /// panel rather than on top of it. Shown only between attempts, when
        /// the wall is empty.</summary>
        private const float ResultsHeightAboveKeys = 0.27f;
        private const float ResultsDepthOffset = 0.0f;

        /// <summary>
        /// The results card (Batch B2, F30 + F32): a world-space Canvas of
        /// its own on Liquid Glass, registered on the keyboard by a
        /// CanvasCalibrationFollower. ResultsCard builds its text and bar
        /// cells in code; this only makes the Canvas and wires it. Created
        /// if missing, otherwise updated; GlassPanel re-created as in
        /// Configure Liquid Glass.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Results Card")]
        public static void ConfigureResultsCard()
        {
            Scene scene = OpenScene();

            var calibration = Object.FindAnyObjectByType<KeyboardCalibration>();
            var scoreKeeper = Object.FindAnyObjectByType<ScoreKeeper>();
            TMPro.TMP_FontAsset font = GlassFont.GetOrCreate();
            if (calibration == null || scoreKeeper == null || font == null)
            {
                Debug.LogError("[SceneSetup] Results card needs KeyboardCalibration, ScoreKeeper and the glass font -- " +
                               "run Configure Scoring first.");
                return;
            }

            Material material = GetOrCreateGlassMaterial();
            PianoGlass.UI.GlassTokens tokens = GetOrCreateGlassTokens();
            if (material == null || tokens == null) return;

            ResultsCard card = Object.FindAnyObjectByType<ResultsCard>();
            GameObject go;
            if (card != null) go = card.gameObject;
            else
            {
                go = new GameObject("ResultsCard", typeof(RectTransform));
                go.AddComponent<Canvas>();
            }

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.transform.localScale = Vector3.one * 0.001f;

            PianoGlass.UI.GlassPanel glass = AddGlass(go, material, tokens);
            // The rect is the card's whole size: its layout is measured to the edge.
            var glassSo = new SerializedObject(glass);
            glassSo.FindProperty("paddingMetres").vector2Value = Vector2.zero;
            glassSo.ApplyModifiedProperties();

            // Editor mouse only; on the headset OvrUiPointer presses the buttons.
            canvas.worldCamera = Camera.main;
            if (go.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null) go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            if (card == null) card = go.AddComponent<ResultsCard>();
            // GlassPanel was re-added after ResultsCard; component order
            // does not matter, ResultsCard looks it up in Awake.

            var cardSo = new SerializedObject(card);
            cardSo.FindProperty("scoreKeeper").objectReferenceValue = scoreKeeper;
            cardSo.FindProperty("transport").objectReferenceValue = Object.FindAnyObjectByType<PianoGlass.Transport.SongTransport>();
            cardSo.FindProperty("calibration").objectReferenceValue = calibration;
            cardSo.FindProperty("tip").objectReferenceValue = Object.FindAnyObjectByType<ControllerTipCalibration>();
            cardSo.FindProperty("statusCanvas").objectReferenceValue = StatusCanvas();
            cardSo.FindProperty("tokens").objectReferenceValue = tokens;
            cardSo.FindProperty("font").objectReferenceValue = font;
            cardSo.FindProperty("textMaterial").objectReferenceValue = GlassTextMaterial.GetOrCreate(font);
            cardSo.FindProperty("glassMaterial").objectReferenceValue = material;
            cardSo.ApplyModifiedProperties();

            CanvasCalibrationFollower follower = go.GetComponent<CanvasCalibrationFollower>();
            if (follower == null) follower = go.AddComponent<CanvasCalibrationFollower>();
            var so = new SerializedObject(follower);
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.FindProperty("depthOffset").floatValue = ResultsDepthOffset;
            so.FindProperty("heightAboveKeys").floatValue = ResultsHeightAboveKeys;
            so.ApplyModifiedProperties();

            ConfigureUiPointer();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SceneSetup] Results card configured ({glass.name}), " +
                      $"{ResultsHeightAboveKeys} m above the keys.");
        }

        private const string PointerLineMaterialPath = "Assets/UI/UiPointerLine.mat";

        /// <summary>The right-controller ray that presses glass buttons, on an
        /// object of its own. Its material is made here, in the editor, and
        /// assigned, so nothing looks a shader up at runtime.</summary>
        private static void ConfigureUiPointer()
        {
            var pointer = Object.FindAnyObjectByType<PianoGlass.UI.OvrUiPointer>();
            GameObject go = pointer != null ? pointer.gameObject : new GameObject("UiPointer");
            if (pointer == null) pointer = go.AddComponent<PianoGlass.UI.OvrUiPointer>();

            var material = AssetDatabase.LoadAssetAtPath<Material>(PointerLineMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "UiPointerLine" };
                material.SetColor("_BaseColor", new Color(0.85f, 0.9f, 0.95f, 1f));
                AssetDatabase.CreateAsset(material, PointerLineMaterialPath);
                AssetDatabase.SaveAssets();
            }

            LineRenderer line = go.GetComponent<LineRenderer>();
            if (line == null) line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.003f;
            line.endWidth = 0.0015f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            var so = new SerializedObject(pointer);
            so.FindProperty("rig").objectReferenceValue = Object.FindAnyObjectByType<OVRCameraRig>();
            so.FindProperty("line").objectReferenceValue = line;
            so.ApplyModifiedProperties();
            if (so.FindProperty("rig").objectReferenceValue == null)
                Debug.LogError("[SceneSetup] No OVRCameraRig in the scene: the UI pointer has nothing to follow.");
        }

        /// <summary>Headset audio output latency, ms: scheduled clip start to
        /// sound out of the speaker. Measured 2026-11-03 from a 60 fps phone
        /// video of the Batch B build: accented clicks landed 141 ms (median
        /// 140, sd 11, 8 bars) after their bar line reached the key line. A
        /// clap video put the phone's own audio/video offset within one
        /// 30 fps frame, so it is not corrected for.</summary>
        private const float MetronomeOutputLatencyMs = 140f;

        /// <summary>
        /// The metronome (Batch B3, F27): a Metronome on its own object, its
        /// measured output latency, and the references SongTransport (A0 +
        /// C#1 toggle), ScoreKeeper (records it per attempt) and the status
        /// panel (shows on/off) hold to it.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Metronome")]
        public static void ConfigureMetronome()
        {
            Scene scene = OpenScene();

            var clock = Object.FindAnyObjectByType<SongClock>();
            if (clock == null)
            {
                Debug.LogError("[SceneSetup] SongClock missing -- run Add Falling-Note Highway first.");
                return;
            }

            Metronome metronome = GetOrAdd<Metronome>("Metronome");
            var so = new SerializedObject(metronome);
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("outputLatencyMs").floatValue = MetronomeOutputLatencyMs;
            so.ApplyModifiedProperties();

            void Point(Component c)
            {
                if (c == null) return;
                var cso = new SerializedObject(c);
                cso.FindProperty("metronome").objectReferenceValue = metronome;
                cso.ApplyModifiedProperties();
            }
            Point(Object.FindAnyObjectByType<SongTransport>());
            Point(Object.FindAnyObjectByType<ScoreKeeper>());
            Point(Object.FindAnyObjectByType<CalibrationPanel>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SceneSetup] Metronome configured, output latency {MetronomeOutputLatencyMs} ms.");
        }

        /// <summary>
        /// The practice loop (Batch C): the component the results card's Loop
        /// button starts, and the references it and the status panel need.
        /// </summary>
        [MenuItem("PianoGlass/Scene/Configure Practice Loop")]
        public static void ConfigurePracticeLoop()
        {
            Scene scene = OpenScene();

            var clock = Object.FindAnyObjectByType<SongClock>();
            var transport = Object.FindAnyObjectByType<SongTransport>();
            var keeper = Object.FindAnyObjectByType<ScoreKeeper>();
            var card = Object.FindAnyObjectByType<ResultsCard>(FindObjectsInactive.Include);
            if (clock == null || transport == null || keeper == null || card == null)
            {
                Debug.LogError("[SceneSetup] SongClock, SongTransport, ScoreKeeper or ResultsCard missing -- " +
                               "run Configure Scoring and Configure Results Card first.");
                return;
            }

            PracticeLoop loop = GetOrAdd<PracticeLoop>("PracticeLoop");
            var so = new SerializedObject(loop);
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("transport").objectReferenceValue = transport;
            so.FindProperty("scoreKeeper").objectReferenceValue = keeper;
            so.FindProperty("resultsCard").objectReferenceValue = card;
            so.ApplyModifiedProperties();

            var panel = Object.FindAnyObjectByType<CalibrationPanel>(FindObjectsInactive.Include);
            if (panel != null)
            {
                var panelSo = new SerializedObject(panel);
                panelSo.FindProperty("loop").objectReferenceValue = loop;
                panelSo.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneSetup] Practice loop configured.");
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

        /// <summary>The status panel's Canvas: the one holding CalibrationPanel.
        /// Not "any Canvas" -- the results card is a second one.</summary>
        private static Canvas StatusCanvas()
        {
            var panel = Object.FindAnyObjectByType<CalibrationPanel>();
            if (panel != null)
            {
                Canvas c = panel.GetComponentInParent<Canvas>();
                if (c != null) return c;
            }
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.GetComponent<ResultsCard>() == null) return c;
            return null;
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
        /// sits HighwayDepthOffset behind the keys -- 10 mm put the hit line
        /// over the key ends (2026-11-02, with a guessed 145 mm key), 20 mm
        /// left a visible gap once the key was measured at 147.64 mm; at
        /// 15 mm FullOverlay photos still showed a clean strip with no real
        /// key ends in it, so 5 mm (2026-11-03) -- just clear of the key
        /// slabs; grid, hit line and slabs all move together -- while the panel keeps
        /// its original spot. The leaned-back runway passes under the panel;
        /// that is accepted, since the panel is a development readout that
        /// the Phase 2/4 menus replace. All set here so the scene cannot keep
        /// a stale override.</summary>
        private const float HighwayDepthOffset = 0.005f;
        private const float PanelDepthOffset = 0.05f;
        private const float PanelHeightAboveKeys = 0.65f;

        private static void WireCanvasFollower(CanvasCalibrationFollower follower, KeyboardCalibration calibration)
        {
            var so = new SerializedObject(follower);
            so.FindProperty("calibration").objectReferenceValue = calibration;
            so.FindProperty("depthOffset").floatValue = PanelDepthOffset;
            so.FindProperty("heightAboveKeys").floatValue = PanelHeightAboveKeys;
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
