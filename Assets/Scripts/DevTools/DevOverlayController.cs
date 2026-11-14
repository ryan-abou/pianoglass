#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.UI;

namespace PianoGlass.DevTools
{
    /// <summary>
    /// Development builds only (compiled out of release): the controls for the
    /// in-headset test checklist and for recording mode.
    ///
    ///   hold left grip, then X   checklist panel on / off (opens in front of you)
    ///   hold left grip, then Y   recording mode on / off
    ///
    /// The grip has to be held for <see cref="HoldSeconds"/> first, and while
    /// it is held X and Y do not do their usual jobs (DevOverlays). Nothing
    /// else reads the left grip, and with the controllers set down on the
    /// piano a squeeze plus a face button on the same hand does not happen
    /// by accident.
    ///
    /// Recording mode is for milestone videos: it switches off -- not fades --
    /// every debug overlay: the status panel (MIDI bridge, calibration
    /// readout, and the old Phase 5 checklist column on it) and this
    /// checklist. What is left is what a player sees of the product: the
    /// highway, the key overlay, the results card, the song picker. It is
    /// remembered across launches, so a relaunch mid-recording stays clean.
    /// Unity's "Development Build" watermark does not reach the headset view
    /// (none in the Quest 3 captures of 2026-11-13), so there is nothing to
    /// hide for it.
    ///
    /// The checklist panel always starts hidden, so it can never be in a
    /// recording by accident. Adds itself after the scene loads; no scene change.
    /// </summary>
    public sealed class DevOverlayController : MonoBehaviour
    {
        /// <summary>The left grip is held this long before X or Y count.</summary>
        public const float HoldSeconds = 0.4f;
        private const string RecordingKey = "dev.recordingMode";

        private GameObject _statusPanel;
        private DevChecklistPanel _checklist;
        private float _gripSince = -1f;
        private bool _recording;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AddInDevelopmentBuilds()
        {
            if (!Debug.isDebugBuild) return;
            var go = new GameObject(nameof(DevOverlayController));
            DontDestroyOnLoad(go);
            go.AddComponent<DevOverlayController>();
        }

        private void Awake()
        {
            // Found once, while still active: switched off, it can't be found again.
            var calibrationPanel = FindAnyObjectByType<CalibrationPanel>();
            Canvas canvas = calibrationPanel != null ? calibrationPanel.GetComponentInParent<Canvas>() : null;
            _statusPanel = canvas != null ? canvas.gameObject : null;
            if (_statusPanel == null) Debug.LogWarning("[DevOverlays] no status panel found; recording mode hides only the checklist");

            // Before the first frame is drawn, so a recording relaunch shows no panel at all.
            _recording = PlayerPrefs.GetInt(RecordingKey, 0) == 1;
            Apply();
            Debug.Log($"[DevOverlays] ready: left grip + X = checklist, left grip + Y = recording mode " +
                      $"(recording mode {(_recording ? "ON" : "off")})");
        }

        private void Update()
        {
            if (!DevOverlays.ModifierHeld)
            {
                _gripSince = -1f;
                return;
            }
            if (_gripSince < 0f) _gripSince = Time.unscaledTime;
            if (Time.unscaledTime - _gripSince < HoldSeconds) return;

            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) ToggleChecklist();
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)) ToggleRecording();
        }

        private void ToggleChecklist()
        {
            if (_recording)
            {
                Debug.Log("[DevOverlays] recording mode is on: the checklist stays hidden (left grip + Y ends it)");
                return;
            }

            if (_checklist == null)
            {
                _checklist = DevChecklistPanel.Create(FindAnyObjectByType<SongPicker>(FindObjectsInactive.Include));
                if (_checklist == null)
                {
                    Debug.LogWarning("[DevOverlays] no song picker to borrow the glass from; no checklist panel");
                    return;
                }
            }

            if (_checklist.IsShown) _checklist.Hide();
            else _checklist.Show(Camera.main != null ? Camera.main.transform : null);
        }

        private void ToggleRecording()
        {
            _recording = !_recording;
            PlayerPrefs.SetInt(RecordingKey, _recording ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            Debug.Log($"[DevOverlays] recording mode {(_recording ? "ON: debug overlays switched off" : "off")}");
        }

        private void Apply()
        {
            if (_statusPanel != null) _statusPanel.SetActive(!_recording);
            if (_recording && _checklist != null) _checklist.Hide();
        }
    }
}
#endif
