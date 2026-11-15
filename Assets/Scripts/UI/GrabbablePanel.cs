using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Piano;

namespace PianoGlass.UI
{
    /// <summary>
    /// A panel the player can pinch and move, tilting it with the hand (#5).
    /// PanelGrabber does the hands; this holds the panel where the hand puts
    /// it, and remembers that spot relative to the keyboard (PlayerPrefs
    /// "panel.&lt;key&gt;"), so the panel comes back there after a relaunch and
    /// moves with the keys when they are nudged or calibrated again.
    ///
    /// Runs after everything that places panels (execution order 1000), so a
    /// saved spot wins over the panel's usual one, and only while the keys are
    /// placed: during a calibration the status panel still comes to your view.
    /// A panel with no key (the test checklist) can be moved but isn't saved.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class GrabbablePanel : MonoBehaviour
    {
        private const string Prefix = "panel.";

        private static readonly List<GrabbablePanel> Shown = new List<GrabbablePanel>();
        private static readonly List<GrabbablePanel> Every = new List<GrabbablePanel>();

        /// <summary>Every panel that is switched on (visible or not).</summary>
        public static IReadOnlyList<GrabbablePanel> All => Shown;

        private string _key;
        private KeyboardCalibration _calibration;
        private Canvas _canvas;
        private System.Action _placeHome;
        private bool _hasSaved;
        private Pose _saved;
        private Pose _target;
        private float _rate;

        private readonly List<GlassPanel> _glass = new List<GlassPanel>();
        private Rect _rect;
        private int _rectFrame = -1;
        private bool _anyGlass;

        public bool Held { get; private set; }
        public string Key => _key;

        /// <summary>Makes <paramref name="go"/> movable. <paramref name="key"/>
        /// null: moved, never saved. <paramref name="placeHome"/> puts it back
        /// in its usual spot, for "Reset panels".</summary>
        public static GrabbablePanel Add(GameObject go, string key, KeyboardCalibration calibration,
                                         System.Action placeHome = null)
        {
            if (go == null) return null;
            GrabbablePanel p = go.GetComponent<GrabbablePanel>();
            if (p == null) p = go.AddComponent<GrabbablePanel>();
            p._key = key;
            p._calibration = calibration;
            p._canvas = go.GetComponent<Canvas>();
            p._placeHome = placeHome;
            p.Load();
            return p;
        }

        /// <summary>Every saved spot forgotten; panels go back to their usual places.</summary>
        public static void ResetAll()
        {
            foreach (GrabbablePanel p in Every)
            {
                if (p == null || p._key == null) continue;
                PlayerPrefs.DeleteKey(Prefix + p._key);
                p._hasSaved = false;
                p._placeHome?.Invoke();
            }
            PlayerPrefs.Save();
            Debug.Log("[Panels] every panel back in its usual place");
        }

        private void Load()
        {
            _hasSaved = _key != null && PanelPose.TryParse(PlayerPrefs.GetString(Prefix + _key, null), out _saved);
            if (_hasSaved) Debug.Log($"[Panels] '{_key}' goes where it was put last time");
        }

        private void Awake() => Every.Add(this);
        private void OnDestroy() => Every.Remove(this);
        private void OnEnable() => Shown.Add(this);

        private void OnDisable()
        {
            Shown.Remove(this);
            if (Held) EndHold();
        }

        /// <summary>Showing right now: its canvas and glass are on.</summary>
        public bool IsVisible
        {
            get
            {
                if (!isActiveAndEnabled || (_canvas != null && !_canvas.enabled)) return false;
                UpdateRect();
                return _anyGlass;
            }
        }

        /// <summary>The panel's outline in its own units (mm): all its glass,
        /// so a results card with its details open counts as one.</summary>
        public Rect Outline
        {
            get
            {
                UpdateRect();
                return _rect;
            }
        }

        private void UpdateRect()
        {
            if (_rectFrame == Time.frameCount) return;
            _rectFrame = Time.frameCount;
            GetComponentsInChildren(false, _glass);
            _anyGlass = false;
            float xMin = 0f, xMax = 0f, yMin = 0f, yMax = 0f;
            var corners = new Vector3[4];
            foreach (GlassPanel g in _glass)
            {
                if (!g.enabled) continue;
                ((RectTransform)g.transform).GetWorldCorners(corners);
                foreach (Vector3 c in corners)
                {
                    Vector3 l = transform.InverseTransformPoint(c);
                    if (!_anyGlass) { xMin = xMax = l.x; yMin = yMax = l.y; }
                    xMin = Mathf.Min(xMin, l.x); xMax = Mathf.Max(xMax, l.x);
                    yMin = Mathf.Min(yMin, l.y); yMax = Mathf.Max(yMax, l.y);
                    _anyGlass = true;
                }
            }
            _rect = _anyGlass ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : ((RectTransform)transform).rect;
        }

        /// <summary>How far a world point is from the face (mm, + in front,
        /// toward the player), and whether it is over the outline within
        /// <paramref name="marginMm"/>.</summary>
        public bool Over(Vector3 world, float marginMm, out float frontMm)
        {
            Vector3 l = transform.InverseTransformPoint(world);
            frontMm = -l.z; // a canvas is read from behind its +Z
            Rect r = Outline;
            return l.x >= r.xMin - marginMm && l.x <= r.xMax + marginMm &&
                   l.y >= r.yMin - marginMm && l.y <= r.yMax + marginMm;
        }

        /// <summary>Where a ray meets the panel's face inside its outline.</summary>
        public bool TryRay(Ray ray, out float distance)
        {
            var plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out distance) || distance <= 0f) return false;
            return Over(ray.GetPoint(distance), 0f, out _);
        }

        public Pose Current => new Pose(transform.position, transform.rotation);

        public void BeginHold(float smoothRate)
        {
            Held = true;
            _rate = smoothRate;
            _target = Current;
        }

        /// <summary>Where the hand wants it this frame; eased there in LateUpdate.</summary>
        public void HoldAt(Pose target) => _target = target;

        public void EndHold()
        {
            if (!Held) return;
            Held = false;
            if (_key == null || !TryKeyboard(out Pose frame)) return;
            _saved = PanelPose.ToFrame(frame, Current);
            _hasSaved = true;
            PlayerPrefs.SetString(Prefix + _key, PanelPose.Format(_saved));
            PlayerPrefs.Save();
            Debug.Log($"[Panels] moved '{_key}' to {_saved.position * 1000f:F0} mm from A0's front edge");
        }

        private void LateUpdate()
        {
            if (Held)
            {
                Pose p = PanelPose.Smooth(Current, _target, Time.unscaledDeltaTime, _rate);
                transform.SetPositionAndRotation(p.position, p.rotation);
                return;
            }
            if (!_hasSaved || !TryKeyboard(out Pose frame)) return;
            Pose saved = PanelPose.FromFrame(frame, _saved);
            transform.SetPositionAndRotation(saved.position, saved.rotation);
        }

        /// <summary>The keyboard's frame: A0's front edge, facing the player.</summary>
        private bool TryKeyboard(out Pose frame)
        {
            frame = default;
            if (_calibration == null || !_calibration.IsPlacementValid) return false;
            PianoKeyLayout layout = _calibration.Layout;
            if (layout == null) return false;
            frame = new Pose(layout.LowFrontEdge, Quaternion.LookRotation(layout.Forward, layout.Up));
            return true;
        }
    }
}
