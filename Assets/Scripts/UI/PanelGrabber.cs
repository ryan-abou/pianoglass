using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// Moves and tilts panels by pinching them (#5), and presses buttons on
    /// panels out of reach, with no controllers:
    ///   - near: pinch the panel's glass or edge and move the hand. The panel
    ///     stays in the hand the way you hold it, so turning the wrist tilts it.
    ///   - far: a ray comes out of the hand when it points at a panel. Pinch
    ///     with the ray on a button to press it, or on the glass to move the
    ///     panel; pulling the hand back while holding brings the panel closer.
    /// Poking a button (HandPokePointer) still presses it; a hand holding a
    /// panel doesn't poke.
    ///
    /// Off while a song plays, so a thumb passing under while playing can't
    /// grab anything, and no panel shows before a launch recenter anyway.
    /// Every move is saved by GrabbablePanel. Logs as "[Panels]". Adds itself
    /// after the scene loads and makes the scene's panels movable; no scene change.
    /// </summary>
    public sealed class PanelGrabber : MonoBehaviour
    {
        /// <summary>Near grab, mm: over the outline within this, and from this
        /// far in front of the face to <see cref="NearBehindMm"/> behind it.</summary>
        private const float NearMarginMm = 30f, NearFrontMm = 60f, NearBehindMm = 40f;
        /// <summary>Metres: the far ray reaches this far.</summary>
        private const float RayReach = 3f;
        /// <summary>A far panel comes this many times as far as the hand is pulled back.</summary>
        private const float PullGain = 3f;
        /// <summary>Metres from the hand a far panel can be pulled to.</summary>
        private const float ClosestPull = 0.25f;
        /// <summary>Per second: how quickly a held panel catches up with the hand.</summary>
        private const float NearRate = 25f, FarRate = 14f;

        private sealed class HandState
        {
            public readonly PinchGrab Pinch = new PinchGrab();
            public GrabbablePanel Held;
            public bool Far;
            public Pose PanelInHand;
            public float StartReach;
            public GlassButton Hovered;
            public LineRenderer Line;
        }

        private readonly HandState[] _hands = { new HandState(), new HandState() };
        private SongTransport _transport;
        private Transform _head;

        /// <summary>Hand <paramref name="h"/> is holding a panel: it doesn't poke.</summary>
        public static bool Holding(int h) =>
            _instance != null && h >= 0 && h < 2 && _instance._hands[h].Held != null;

        private static PanelGrabber _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            var go = new GameObject(nameof(PanelGrabber));
            go.AddComponent<PanelGrabber>();
        }

        private void Awake() => _instance = this;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            _transport = FindAnyObjectByType<SongTransport>();
            var calibration = FindAnyObjectByType<KeyboardCalibration>();

            // The scene's own panels: the status panel, the results card and the song picker.
            foreach (CanvasCalibrationFollower f in FindObjectsByType<CanvasCalibrationFollower>(FindObjectsInactive.Include))
            {
                CanvasCalibrationFollower follower = f;
                GrabbablePanel.Add(f.gameObject, f.gameObject.name, calibration, follower.PlaceNow);
            }

            LineRenderer template = null;
            OvrUiPointer pointer = FindAnyObjectByType<OvrUiPointer>(FindObjectsInactive.Include);
            if (pointer != null) template = pointer.GetComponentInChildren<LineRenderer>(true);
            for (int h = 0; h < 2; h++) _hands[h].Line = MakeLine(template, h);
        }

        private LineRenderer MakeLine(LineRenderer template, int h)
        {
            if (template == null || template.sharedMaterial == null) return null;
            var go = new GameObject($"HandRay {h}");
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = template.sharedMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = template.startWidth;
            line.endWidth = template.endWidth;
            line.startColor = template.startColor;
            line.endColor = template.endColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private bool SongRunning
        {
            get
            {
                if (_transport == null) return false;
                SongTransport.State s = _transport.CurrentState;
                return s == SongTransport.State.Playing || s == SongTransport.State.CountingIn;
            }
        }

        private void LateUpdate()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            TrackedHands hands = TrackedHands.Instance;
            bool off = SongRunning || LaunchResync.WaitingForRecenter;
            for (int h = 0; h < 2; h++) Step(h, hands, off);
        }

        private void Step(int h, TrackedHands hands, bool off)
        {
            HandState s = _hands[h];
            Vector3 point = default;
            Quaternion turn = Quaternion.identity;
            bool pinching = false;
            Ray ray = default;
            bool tracked = !off && hands != null && hands.TryPinch(h, out point, out turn, out pinching);
            bool hasRay = tracked && hands.TryPointer(h, out ray);

            // What the hand is on, when it isn't holding anything.
            GrabbablePanel near = null, far = null;
            GlassButton button = null;
            float farDistance = 0f;
            if (tracked && s.Held == null)
            {
                near = Nearest(point, out bool close);
                // A hand right at a panel pokes or grabs it; the ray is for out of reach.
                if (!close && hasRay) far = Pointed(ray, out farDistance, out button);
            }

            PinchGrab.Step step = s.Pinch.Update(tracked, pinching, near != null || far != null, Time.unscaledDeltaTime);
            if (step == PinchGrab.Step.Start)
            {
                if (button != null)
                {
                    s.Pinch.Release();
                    button.Press();
                    Debug.Log($"[Hands] pinched '{button.name}' from afar");
                }
                else Begin(s, near ?? far, near == null, point, turn, ray);
            }
            else if (step == PinchGrab.Step.Hold && s.Held != null && tracked)
                Move(s, point, turn, ray, hasRay);
            else if (step == PinchGrab.Step.End || (s.Held != null && (off || !s.Held.isActiveAndEnabled)))
                End(s);

            SetHover(s, s.Held == null ? button : null);
            DrawRay(s, hasRay && s.Held == null && far != null, ray, farDistance,
                    s.Held != null && s.Far && hasRay);
        }

        /// <summary>The visible panel whose face the pinch is closest to, not
        /// counting a pinch over a button. <paramref name="close"/>: the hand
        /// is at some panel, button or not.</summary>
        private static GrabbablePanel Nearest(Vector3 point, out bool close)
        {
            GrabbablePanel best = null;
            float bestFront = 0f;
            close = false;
            foreach (GrabbablePanel p in GrabbablePanel.All)
            {
                if (!p.IsVisible || !p.Over(point, NearMarginMm, out float front)) continue;
                if (front > NearFrontMm || front < -NearBehindMm) continue;
                close = true;
                // Up close a button is for poking: grab the glass around it or the edge.
                if (OverButton(p, point)) continue;
                if (best == null || Mathf.Abs(front) < Mathf.Abs(bestFront))
                {
                    best = p;
                    bestFront = front;
                }
            }
            return best;
        }

        private static bool OverButton(GrabbablePanel panel, Vector3 point)
        {
            foreach (GlassButton b in GlassButton.All)
            {
                if (!b.IsPressable || !b.transform.IsChildOf(panel.transform)) continue;
                var rect = (RectTransform)b.transform;
                Vector3 l = rect.InverseTransformPoint(point);
                Rect r = rect.rect;
                if (l.x >= r.xMin - 4f && l.x <= r.xMax + 4f && l.y >= r.yMin - 4f && l.y <= r.yMax + 4f) return true;
            }
            return false;
        }

        /// <summary>The nearest visible panel the ray meets, and the button it
        /// is on if any.</summary>
        private static GrabbablePanel Pointed(Ray ray, out float distance, out GlassButton button)
        {
            GrabbablePanel best = null;
            distance = RayReach;
            button = null;
            foreach (GrabbablePanel p in GrabbablePanel.All)
                if (p.IsVisible && p.TryRay(ray, out float d) && d < distance)
                {
                    best = p;
                    distance = d;
                }
            if (best == null) return null;

            float nearest = float.PositiveInfinity;
            foreach (GlassButton b in GlassButton.All)
            {
                if (!b.IsPressable || !b.transform.IsChildOf(best.transform)) continue;
                if (b.TryHit(ray, out float d) && d < nearest)
                {
                    nearest = d;
                    button = b;
                }
            }
            return best;
        }

        private Pose HandPose(HandState s, Vector3 point, Quaternion turn, Ray ray) =>
            s.Far ? PanelPose.RayPose(ray.origin, ray.direction) : new Pose(point, turn);

        private float Reach(Vector3 point) => _head != null ? Vector3.Distance(_head.position, point) : 0f;

        private void Begin(HandState s, GrabbablePanel panel, bool far, Vector3 point, Quaternion turn, Ray ray)
        {
            s.Held = panel;
            s.Far = far;
            s.PanelInHand = PanelPose.ToFrame(HandPose(s, point, turn, ray), panel.Current);
            s.StartReach = Reach(point);
            panel.BeginHold(far ? FarRate : NearRate);
            Debug.Log($"[Panels] picked up '{panel.name}' {(far ? "from afar" : "by hand")}");
        }

        private void Move(HandState s, Vector3 point, Quaternion turn, Ray ray, bool hasRay)
        {
            if (s.Far && !hasRay) return; // keep it where it is until the ray is back
            Pose target = PanelPose.FromFrame(HandPose(s, point, turn, ray), s.PanelInHand);
            if (s.Far)
            {
                // Pulling the hand in brings the panel in, faster than the hand moves.
                float pull = (Reach(point) - s.StartReach) * PullGain;
                Vector3 along = target.position - ray.origin;
                float length = Mathf.Max(ClosestPull, along.magnitude + pull);
                target.position = ray.origin + along.normalized * length;
            }
            s.Held.HoldAt(target);
        }

        private static void End(HandState s)
        {
            if (s.Held == null) return;
            GrabbablePanel panel = s.Held;
            s.Held = null;
            s.Pinch.Release();
            panel.EndHold();
        }

        private static void SetHover(HandState s, GlassButton b)
        {
            if (s.Hovered == b) return;
            if (s.Hovered != null) s.Hovered.SetHovered(false);
            s.Hovered = b;
            if (b != null) b.SetHovered(true);
        }

        private static void DrawRay(HandState s, bool pointing, Ray ray, float distance, bool holdingFar)
        {
            if (s.Line == null) return;
            bool show = pointing || holdingFar;
            s.Line.enabled = show;
            if (!show) return;
            Vector3 end = holdingFar ? s.Held.Current.position : ray.GetPoint(distance);
            s.Line.SetPosition(0, ray.origin);
            s.Line.SetPosition(1, end);
        }
    }
}
