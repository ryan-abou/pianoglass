using PianoGlass.Highway;
using PianoGlass.Piano;
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
    /// Idempotent -- run it again after changing a script and it updates the
    /// existing objects instead of duplicating them.
    ///
    /// Doing this by hand in the Inspector is exactly the kind of setup that
    /// silently drifts between machines and gets forgotten right before a
    /// build.
    /// </summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string NoteMaterialPath = "Assets/NoteHighway.mat";

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

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[SceneSetup] SongClock + NoteHighway + SongTransport wired into " +
                      $"{scene.name} and saved.");
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
    }
}
