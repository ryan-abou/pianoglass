using System;
using System.IO;
using TMPro;
using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Transport;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Development builds: the Phase 5 headset checklist (DeviceChecklist)
    /// on a column beside the calibration panel, so the tester never takes
    /// the headset off to read the next step. Feeds it the flow's
    /// transitions, head height, transport starts, anchor pose jumps
    /// (recenters) and MIDI notes; all the deciding is in DeviceChecklist.
    ///
    /// Added at runtime by CalibrationPanel -- no scene change. Progress is
    /// kept in device_checklist.json and starts over with each new build.
    /// To start over on the same build:
    ///   adb shell rm /sdcard/Android/data/com.ryanabourjeili.pianoglass/files/device_checklist.json
    /// </summary>
    public class DeviceChecklistRunner : MonoBehaviour
    {
        public const string FileName = "device_checklist.json";

        private CalibrationFlowController _flow;
        private SongTransport _transport;
        private TMP_Text _text;

        private AnchoredCalibrationSession _session;
        private DeviceChecklist _list;
        private string _path;
        private int[] _logged = new int[DeviceChecklist.Count];
        private bool _wasRunning;
        private string _anchorId;
        private Pose _anchorPose;
        private float _nextRender;

        /// <summary>Called by CalibrationPanel right after AddComponent.
        /// <paramref name="style"/> is the panel's own text: the column copies
        /// its font and sits on its Canvas.</summary>
        public void Init(CalibrationFlowController flow, SongTransport transport, TMP_Text style)
        {
            _flow = flow;
            _transport = transport;

            Canvas canvas = style.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("DeviceChecklist", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            // A column to the right of the Canvas, full height, top-aligned.
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(30f, 0f);
            rt.sizeDelta = new Vector2(480f, 0f);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = style.font;
            t.fontSize = style.fontSize;
            t.color = style.color;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            _text = t;
        }

        private void Start()
        {
            // The controller's Awake has run by now: Active is settled.
            if (_flow == null || !_flow.Active || _text == null)
            {
                if (_text != null) Destroy(_text.gameObject);
                enabled = false;
                return;
            }

            _session = _flow.Session;
            _path = Path.Combine(Application.persistentDataPath, FileName);
            DeviceChecklistState restored = null;
            try
            {
                if (File.Exists(_path)) restored = JsonUtility.FromJson<DeviceChecklistState>(File.ReadAllText(_path));
            }
            catch (Exception e)
            {
                Debug.Log($"[AnchorFlow] checklist: could not read {_path} ({e.Message}); starting over");
            }

            _list = new DeviceChecklist(restored, Application.buildGUID);
            Array.Copy(_list.State.results, _logged, DeviceChecklist.Count);
            Debug.Log($"[AnchorFlow] checklist: run {_list.State.run}, {_list.PassedCount}/{DeviceChecklist.Count} passed");

            // The launch profile is read in the controller's Start, which may
            // already have run; its lastCorrection is kept on the session.
            _list.OnLaunchProfile(_session.LaunchLastCorrection);
            _session.Transitioned += OnTransition;
            if (MidiInput.Instance != null) MidiInput.Instance.NoteOn += OnNoteOn;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Transitioned -= OnTransition;
            if (MidiInput.Instance != null) MidiInput.Instance.NoteOn -= OnNoteOn;
        }

        private void OnTransition(FlowEvent e, FlowTransition t)
        {
            if (e == FlowEvent.LocalizeSucceeded) _list.OnLaunchProfile(_session.LaunchLastCorrection);
            _list.OnFlow(e, t, _session.LastVerifyErrorMm, Time.realtimeSinceStartupAsDouble);
        }

        private void OnNoteOn(int note, int velocity, double time) => _list.OnNoteOn(_session.Flow.State);

        private void Update()
        {
            FlowState state = _session.Flow.State;

            Camera cam = Camera.main;
            if (cam != null) _list.OnHead(cam.transform.position.y, _session.IsOverlayVisible);

            bool running = _transport != null &&
                           (_transport.CurrentState == SongTransport.State.CountingIn ||
                            _transport.CurrentState == SongTransport.State.Playing);
            if (running && !_wasRunning) _list.OnTransportStarted(state, Time.realtimeSinceStartupAsDouble);
            _wasRunning = running;

            // Same anchor, big one-frame move: a recenter. The last tracked
            // pose is kept through tracking loss, so a recenter that briefly
            // drops tracking is still seen when it comes back.
            IPianoAnchorHandle a = _session.Anchor;
            if (a != null && a.IsAlive && a.IsTracked)
            {
                Pose p = a.Pose;
                if (a.Id == _anchorId)
                    _list.OnAnchorJump(Vector3.Distance(p.position, _anchorPose.position),
                                       Quaternion.Angle(p.rotation, _anchorPose.rotation), state);
                _anchorId = a.Id;
                _anchorPose = p;
            }

            for (int i = 0; i < DeviceChecklist.Count; i++)
            {
                if (_list.State.results[i] == _logged[i]) continue;
                _logged[i] = _list.State.results[i];
                Debug.Log($"[AnchorFlow] checklist: step {i + 1} ({DeviceChecklist.Titles[i]}) {(CheckResult)_logged[i]}");
            }

            if (_list.Dirty) Save();

            if (Time.unscaledTime >= _nextRender)
            {
                _nextRender = Time.unscaledTime + 0.25f;
                _text.text = _list.Render();
            }
        }

        private void Save()
        {
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(_list.State));
                _list.MarkSaved();
            }
            catch (Exception e)
            {
                Debug.Log($"[AnchorFlow] checklist: not saved ({e.Message})");
                _list.MarkSaved(); // do not retry every frame
            }
        }
    }
}
