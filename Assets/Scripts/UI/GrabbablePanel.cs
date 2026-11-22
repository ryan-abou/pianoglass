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
        /// <summary>Let go of while the keys were not placed: saved, relative
        /// to the keys, as soon as they are (round 11: a panel moved then
        /// went back to its old spot).</summary>
        private bool _pendingSave;
        private Pose _saved;
        private Pose _target;
        private float _rate;

        private readonly List<GlassPanel> _glass = new List<GlassPanel>();
        private Rect _rect;
        private int _rectFrame = -1;
        private bool _anyGlass;

        public bool Held { get; private set; }

        /// <summary>Let go of: where it was put is final (settings hands it to the song panel).</summary>
        public event System.Action<GrabbablePanel> Released;
        /// <summary>It has a spot of its own: whoever places it by default leaves it alone.</summary>
        public bool HasSaved => _hasSaved;
        /// <summary>Saved, or waiting to be: whoever places it by default leaves it alone.</summary>
        public bool KeepsItsOwnSpot => _hasSaved || _pendingSave;
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
                p._pendingSave = false;
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

        /// <summary>Its glass surfaces this frame (shown or not), for the snap glow.</summary>
        public IReadOnlyList<GlassPanel> Glass
        {
            get
            {
                UpdateRect();
                return _glass;
            }
        }

        /// <summary>Where <paramref name="glass"/> sits in this panel's own units (mm).</summary>
        public Rect RectOf(GlassPanel glass)
        {
            var corners = new Vector3[4];
            ((RectTransform)glass.transform).GetWorldCorners(corners);
            Vector3 a = transform.InverseTransformPoint(corners[0]), b = transform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
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

        /// <param name="at">Where it ends up, if not where it is now: letting
        /// go puts it back where it was before the fingers opened.</param>
        public void EndHold(Pose? at = null)
        {
            if (!Held) return;
            Held = false;
            if (at.HasValue) transform.SetPositionAndRotation(at.Value.position, at.Value.rotation);
            Save();
            Released?.Invoke(this);
        }

        /// <summary>Puts it at <paramref name="world"/> and keeps it there, as if moved by hand.</summary>
        public void PutAt(Pose world)
        {
            transform.SetPositionAndRotation(world.position, world.rotation);
            Save();
        }

        private void Save()
        {
            if (_key == null) return;
            if (!TryKeyboard(out Pose frame))
            {
                _pendingSave = true;
                Debug.Log($"[Panels] '{_key}' moved before the keys are placed: saved once they are");
                return;
            }
            _pendingSave = false;
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
            if (_pendingSave && TryKeyboard(out _)) Save();
            TryPlaceSaved();
        }

        /// <summary>Puts it where it was saved, on the keys as they are now.
        /// For a panel that is switched off, which doesn't run LateUpdate:
        /// the song panel while settings takes its place (2026-11-20: settings
        /// jumped back to the usual spot after the keys moved a little).</summary>
        public bool TryPlaceSaved()
        {
            if (Held || !_hasSaved || !TryKeyboard(out Pose frame)) return false;
            Pose saved = PanelPose.FromFrame(frame, _saved);
            transform.SetPositionAndRotation(saved.position, saved.rotation);
            return true;
        }

        /// <summary>The keyboard's frame: A0's front edge, +Z toward the player.</summary>
        public bool TryKeyboard(out Pose frame)
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
