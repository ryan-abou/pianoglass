using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Development-build-only test harness for spatial anchors (P-01..P-03).
    /// Not the anchored-calibration feature: nothing reads what it writes.
    /// Since Phase 4 the component is disabled in the scene (SceneSetup,
    /// Configure Anchored Calibration) -- its grips would clash with the
    /// Phase 5 corrections -- and the code is kept. It existed to answer, on the
    /// real headset, "does an anchor saved at the piano come back on the
    /// piano after a relaunch / recenter / reboot, and how long does it take".
    ///
    /// Disables itself in release builds. Never creates or loads anything on
    /// launch -- every anchor call is a deliberate button press:
    ///
    ///   Right grip (release)        create + save an anchor at the current
    ///                               keyboard's centre; record its UUID and the
    ///                               keyboard in anchor-local coordinates
    ///   Left grip (release)         load + localize that UUID, show three
    ///                               markers (A0 / C4 / C8) from the anchor,
    ///                               log how far they are from today's
    ///                               legacy calibration
    ///   Both grips held 1.5 s       erase that UUID
    ///
    /// A grip that was part of a both-grip hold does not also fire its
    /// single action on release. Inputs are ignored while a call is running
    /// and while the controller tip is being measured.
    ///
    /// State: Application.persistentDataPath/anchor_spike.json. Only ever
    /// the UUID and anchor-local geometry -- never a world position. A
    /// failed load or localize leaves it, and the anchor, alone.
    ///
    /// Every log line starts with "[Anchor]"; read them with
    /// adb logcat -s Unity.
    /// </summary>
    public class OvrAnchorSpike : MonoBehaviour
    {
        public const string FileName = "anchor_spike.json";

        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private ControllerTipCalibration tip;

        [Tooltip("Unlit material for the A0 / C4 / C8 markers. Assigned by " +
                 "SceneSetup; a runtime Shader.Find would be stripped from the build.")]
        [SerializeField] private Material markerMaterial;

        [SerializeField] private double localizeTimeoutSeconds = 10;
        [SerializeField] private float eraseHoldSeconds = 1.5f;
        [SerializeField] private float gripThreshold = 0.6f;

        [Tooltip("A per-frame jump of the anchor's world pose bigger than this is " +
                 "logged as a tracking-space change (recenter, or a relocalization).")]
        [SerializeField] private float jumpMetres = 0.05f;
        [SerializeField] private float jumpDegrees = 2f;

        private static readonly int[] CheckNotes = { 21, 60, 108 };

        [Serializable]
        private class SpikeState
        {
            public string uuid;
            public bool saved;
            public bool erased;
            public string createdUtc;
            public int lowestNote;
            public int highestNote;
            public float whiteKeyDepth;
            public Vector3 lowFrontLocal;
            public Vector3 highFrontLocal;
            public Vector3 towardPlayerLocal;
        }

        private IPianoAnchorService _service;
        private SpikeState _state;
        private string _path;
        private bool _busy;
        private bool _stateUnreadable;

        private IPianoAnchorHandle _handle;
        private Transform[] _markers;
        private Pose _lastPose;
        private bool _lastTracked;

        private bool _leftWasDown, _rightWasDown, _combo, _erasedThisHold;
        private float _bothSince = -1f;

        private void Awake()
        {
            if (!Debug.isDebugBuild)
            {
                enabled = false;
                return;
            }
            _service = new OvrPianoAnchorService();
            _path = Path.Combine(Application.persistentDataPath, FileName);
            if (calibration == null) calibration = GetComponent<KeyboardCalibration>();
            if (tip == null) tip = GetComponent<ControllerTipCalibration>();
        }

        private void Start()
        {
            _state = ReadState();
            Debug.Log($"[Anchor] spike ready: stored={(_state?.uuid ?? "none")} " +
                      $"saved={_state?.saved ?? false} erased={_state?.erased ?? false} " +
                      $"created={_state?.createdUtc ?? "-"} {XrState()}");
        }

        private void Update()
        {
            TrackHandle();
            HandleInput();
        }

        // ------------------------------------------------------------------
        // input

        private void HandleInput()
        {
            bool left = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) > gripThreshold;
            bool right = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.RTouch) > gripThreshold;
            bool blocked = _busy || (tip != null && tip.IsBusy);

            if (left && right)
            {
                _combo = true;
                if (_bothSince < 0f) _bothSince = Time.unscaledTime;
                if (!_erasedThisHold && !blocked && Time.unscaledTime - _bothSince >= eraseHoldSeconds)
                {
                    _erasedThisHold = true;
                    _ = Run(EraseStored);
                }
            }
            else
            {
                _bothSince = -1f;
            }

            if (!blocked && !_combo)
            {
                if (_rightWasDown && !right) _ = Run(CreateAndSave);
                if (_leftWasDown && !left) _ = Run(LoadAndShow);
            }

            if (!left && !right) { _combo = false; _erasedThisHold = false; }
            _leftWasDown = left;
            _rightWasDown = right;
        }

        private async Task Run(Func<Task> action)
        {
            _busy = true;
            try { await action(); }
            catch (Exception e) { Debug.LogError($"[Anchor] harness exception {e}"); }
            finally { _busy = false; }
        }

        // ------------------------------------------------------------------
        // actions

        private async Task CreateAndSave()
        {
            if (_stateUnreadable)
            {
                Debug.Log($"[Anchor] create refused: {FileName} exists but could not be read -- pull it with adb first");
                return;
            }
            if (_state != null && !string.IsNullOrEmpty(_state.uuid) && !_state.erased)
            {
                Debug.Log($"[Anchor] create refused: spike anchor {_state.uuid} not erased yet " +
                          "-- hold both grips 1.5 s to erase it first");
                return;
            }
            PianoKeyLayout layout = calibration != null ? calibration.Layout : null;
            if (layout == null || !calibration.IsPlacementValid)
            {
                Debug.Log("[Anchor] create refused: no valid keyboard placement -- calibrate first");
                return;
            }

            Pose frame = AnchoredKeyboard.Frame(layout.LowFrontEdge, layout.HighFrontEdge, layout.Forward);
            Debug.Log($"[Anchor] create requested {XrState()}");
            AnchorResult created = await _service.CreateAsync(frame.position, frame.rotation);
            Debug.Log($"[Anchor] create {created}");
            if (!created.Ok) return;

            // Store the geometry relative to the pose the runtime actually
            // gave the anchor, not the one asked for.
            Pose actual = created.Handle.Pose;
            Debug.Log($"[Anchor] create pose delta vs requested: " +
                      $"{(actual.position - frame.position).magnitude * 1000f:0.0} mm, " +
                      $"{Quaternion.Angle(actual.rotation, frame.rotation):0.00} deg");

            KeyboardGeometry local = AnchoredKeyboard.ToLocal(actual, layout.LowFrontEdge, layout.HighFrontEdge, layout.Forward);
            _state = new SpikeState
            {
                uuid = created.Uuid,
                saved = false,
                erased = false,
                createdUtc = DateTime.UtcNow.ToString("o"),
                lowestNote = layout.LowestNote,
                highestNote = layout.HighestNote,
                whiteKeyDepth = layout.WhiteKeyDepth,
                lowFrontLocal = local.LowFront,
                highFrontLocal = local.HighFront,
                towardPlayerLocal = local.TowardPlayer,
            };
            // Written before the save so an unsaved-but-created UUID can
            // still be erased by hand if the save half-happens.
            WriteState();
            Attach(created.Handle);

            AnchorResult saved = await _service.SaveAsync(created.Handle);
            Debug.Log($"[Anchor] save {saved}");
            _state.saved = saved.Ok;
            WriteState();
        }

        private async Task LoadAndShow()
        {
            if (_state == null || string.IsNullOrEmpty(_state.uuid) || _state.erased)
            {
                Debug.Log("[Anchor] load refused: no stored spike anchor -- right grip creates one");
                return;
            }

            Debug.Log($"[Anchor] load requested uuid={_state.uuid} t={Time.realtimeSinceStartup:0.0}s {XrState()}");
            AnchorResult loaded = await _service.LoadAsync(_state.uuid, localizeTimeoutSeconds);
            Debug.Log($"[Anchor] load {loaded}");
            if (!loaded.Ok) return;   // never erase or rewrite state on failure

            Attach(loaded.Handle);
            LogComparison(loaded.Handle.Pose);
        }

        private async Task EraseStored()
        {
            if (_state == null || string.IsNullOrEmpty(_state.uuid) || _state.erased)
            {
                Debug.Log("[Anchor] erase: nothing stored");
                return;
            }

            AnchorResult erased = _handle != null && _handle.IsAlive && _handle.Id == _state.uuid
                ? await _service.EraseAsync(_handle)
                : await _service.EraseByUuidAsync(_state.uuid);
            Debug.Log($"[Anchor] erase {erased}");
            if (!erased.Ok) return;

            _state.erased = true;
            WriteState();
            Detach();
        }

        // ------------------------------------------------------------------
        // markers and diagnostics

        private KeyboardGeometry StoredLocal() =>
            new KeyboardGeometry(_state.lowFrontLocal, _state.highFrontLocal, _state.towardPlayerLocal);

        private PianoKeyLayout AnchoredLayout(Pose anchorPose) =>
            AnchoredKeyboard.BuildLayout(anchorPose, StoredLocal(), _state.lowestNote, _state.highestNote,
                                         _state.whiteKeyDepth, Vector3.zero);

        private void LogComparison(Pose anchorPose)
        {
            PianoKeyLayout anchored = AnchoredLayout(anchorPose);
            string plausible = AnchoredKeyboard.CheckPlausible(StoredLocal(), _state.lowestNote, _state.highestNote);

            PianoKeyLayout legacy = calibration != null ? calibration.Layout : null;
            if (legacy == null)
            {
                Debug.Log($"[Anchor] compare: no legacy layout this session; anchored spacing " +
                          $"{anchored.WhiteKeySpacing * 1000f:0.00} mm, plausible={plausible ?? "ok"}");
                return;
            }

            var sb = new System.Text.StringBuilder("[Anchor] compare anchored-vs-legacy:");
            foreach (int note in CheckNotes)
            {
                if (!anchored.Contains(note) || !legacy.Contains(note)) continue;
                Vector3 d = anchored.FrontEdgeCenter(note) - legacy.FrontEdgeCenter(note);
                sb.Append($" n{note}={d.magnitude * 1000f:0.0}mm(dy {d.y * 1000f:+0.0;-0.0}mm)");
            }
            sb.Append($" yaw={AnchoredKeyboard.YawBetween(legacy.Right, anchored.Right):+0.00;-0.00} deg");
            sb.Append($" plausible={plausible ?? "ok"} tracked={_handle?.IsTracked}");
            Debug.Log(sb.ToString());
        }

        private void Attach(IPianoAnchorHandle handle)
        {
            _handle = handle;
            _lastPose = handle.Pose;
            _lastTracked = handle.IsTracked;
            EnsureMarkers();
            PlaceMarkers();
        }

        private void Detach()
        {
            _handle = null;
            if (_markers == null) return;
            foreach (Transform m in _markers) if (m) m.gameObject.SetActive(false);
        }

        private void TrackHandle()
        {
            if (_handle == null) return;
            if (!_handle.IsAlive)
            {
                Debug.Log($"[Anchor] handle {_handle.Id} died");
                Detach();
                return;
            }

            Pose p = _handle.Pose;
            float moved = (p.position - _lastPose.position).magnitude;
            float turned = Quaternion.Angle(p.rotation, _lastPose.rotation);
            if (moved > jumpMetres || turned > jumpDegrees)
                Debug.Log($"[Anchor] anchor world pose jumped {moved * 1000f:0} mm / {turned:0.0} deg in one frame " +
                          "(tracking space re-based -- recenter or relocalization)");
            if (_handle.IsTracked != _lastTracked)
                Debug.Log($"[Anchor] tracked={_handle.IsTracked}");

            _lastPose = p;
            _lastTracked = _handle.IsTracked;
            PlaceMarkers();
        }

        private void EnsureMarkers()
        {
            if (_markers != null) { foreach (Transform m in _markers) if (m) m.gameObject.SetActive(true); return; }

            _markers = new Transform[CheckNotes.Length];
            for (int i = 0; i < CheckNotes.Length; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"AnchorSpikeMarker n{CheckNotes[i]}";
                Destroy(cube.GetComponent<Collider>());
                if (markerMaterial != null) cube.GetComponent<Renderer>().sharedMaterial = markerMaterial;
                // 6 mm wide, 40 mm tall, 6 mm deep: a post standing on the
                // key's front edge, easy to judge left/right against.
                cube.transform.localScale = new Vector3(0.006f, 0.04f, 0.006f);
                _markers[i] = cube.transform;
            }
        }

        private void PlaceMarkers()
        {
            if (_handle == null || _markers == null || _state == null) return;
            PianoKeyLayout anchored = AnchoredLayout(_handle.Pose);
            for (int i = 0; i < CheckNotes.Length; i++)
            {
                if (!_markers[i] || !anchored.Contains(CheckNotes[i])) continue;
                _markers[i].SetPositionAndRotation(
                    anchored.FrontEdgeCenter(CheckNotes[i]) + anchored.Up * 0.02f, anchored.Rotation);
            }
        }

        private static string XrState()
        {
            OVRManager mgr = OVRManager.instance;
            return $"hmd={OVRManager.isHmdPresent} focus={OVRManager.hasInputFocus} " +
                   $"origin={(mgr != null ? mgr.trackingOriginType.ToString() : "no-OVRManager")}";
        }

        // ------------------------------------------------------------------
        // state file

        private SpikeState ReadState()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var s = JsonUtility.FromJson<SpikeState>(File.ReadAllText(_path));
                return s != null && !string.IsNullOrEmpty(s.uuid) ? s : null;
            }
            catch (Exception e)
            {
                // Leave the file where it is, and refuse to create (which would
                // overwrite it): a bad read is not a reason to lose a UUID.
                _stateUnreadable = true;
                Debug.LogWarning($"[Anchor] could not read {_path}: {e.Message} -- create disabled this session");
                return null;
            }
        }

        private void WriteState()
        {
            try
            {
                string tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(_state, true));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(tmp, _path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Anchor] could not write {_path}: {e.Message}");
            }
        }
    }
}
