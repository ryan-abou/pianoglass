using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.Transport;

namespace PianoGlass.UI
{
    /// <summary>
    /// Moves and tilts panels (#5), and presses buttons on panels out of reach:
    ///   - near: pinch the panel's glass or edge and move the hand. The panel
    ///     stays in the hand the way you hold it, so turning the wrist tilts it.
    ///   - far: a ray comes out of the hand when it points at a panel. Pinch
    ///     with the ray on a button to press it, or on the glass to move the
    ///     panel; pulling the hand back while holding brings the panel closer.
    ///   - controller: the right controller's ray on a panel's glass, hold the
    ///     trigger to move it the same way (Ryan, 2026-11-20).
    /// Poking a button (HandPokePointer) still presses it; a hand holding a
    /// panel doesn't poke.
    ///
    /// A pinch starts a hold the way PinchGrab decides; after that, PinchHold
    /// decides from the gap between thumb and index tips (the Quest's own
    /// yes/no dropped panels, 2026-11-20). A held panel follows the wrist,
    /// and letting go puts it back where it was the moment the fingers began
    /// to open, so letting go never drags it.
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
        /// <summary>The right controller's slot after the two hands.</summary>
        private const int ControllerSlot = 2;

        private sealed class HandState
        {
            public readonly PinchGrab Pinch = new PinchGrab();
            public readonly PinchHold Hold = new PinchHold();
            public readonly PoseTrail Trail = new PoseTrail();
            public GrabbablePanel Held;
            public bool Far;
            public Pose PanelInHand;
            public float StartReach, HeldSince, LostFor;
            public PanelPose.Snap Snap;
            public int Losses;
            public GlassButton Hovered;
            public LineRenderer Line;
        }

        private readonly HandState[] _hands = { new HandState(), new HandState(), new HandState() };
        private SnapGuide _guide;
        private SongTransport _transport;
        private KeyboardCalibration _calibration;
        private CalibrationFlowController _flow;
        private Transform _head;

        /// <summary>Hand <paramref name="h"/> is holding a panel: it doesn't poke.</summary>
        public static bool Holding(int h) =>
            _instance != null && h >= 0 && h < 2 && _instance._hands[h].Held != null;

        /// <summary>The controller ray is on a panel's glass or holding one:
        /// its trigger is for the panel, and the ray ends at
        /// <see cref="ControllerRayEnd"/>.</summary>
        public static bool ControllerOnPanel { get; private set; }
        public static Vector3 ControllerRayEnd { get; private set; }
        /// <summary>The controller is holding a panel.</summary>
        public static bool ControllerHolding =>
            _instance != null && _instance._hands[ControllerSlot].Held != null;

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
            ControllerOnPanel = false;
        }

        private void Start()
        {
            _transport = FindAnyObjectByType<SongTransport>();
            _calibration = FindAnyObjectByType<KeyboardCalibration>();
            _flow = FindAnyObjectByType<CalibrationFlowController>();

            // The scene's own panels: the status panel, the results card and the song picker.
            foreach (CanvasCalibrationFollower f in FindObjectsByType<CanvasCalibrationFollower>(FindObjectsInactive.Include))
            {
                CanvasCalibrationFollower follower = f;
                GrabbablePanel.Add(f.gameObject, f.gameObject.name, _calibration, follower.PlaceNow);
            }

            LineRenderer template = null;
            OvrUiPointer pointer = FindAnyObjectByType<OvrUiPointer>(FindObjectsInactive.Include);
            if (pointer != null) template = pointer.GetComponentInChildren<LineRenderer>(true);
            for (int h = 0; h < 2; h++) _hands[h].Line = MakeLine(template, h);
            var keys = FindAnyObjectByType<PianoKeyHighlighter>(FindObjectsInactive.Include);
            _guide = SnapGuide.Create(transform, keys != null ? keys.TranslucentMaterial : null);
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

        /// <summary>The trigger also places calibration points and the Middle
        /// C fix: panels only take it once the keys are calibrated and no fix
        /// is waiting for a touch.</summary>
        private bool TriggerFree =>
            _calibration != null && _calibration.CurrentStage == KeyboardCalibration.Stage.Calibrated &&
            !(_flow != null && _flow.Active && _flow.IsCorrecting);

        private void LateUpdate()
        {
            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            TrackedHands hands = TrackedHands.Instance;
            bool off = SongRunning || LaunchResync.WaitingForRecenter;
            for (int h = 0; h < 2; h++) Step(h, hands, off);
            StepController(off);

            // The snap guide, on the first held panel.
            HandState held = null;
            foreach (HandState s in _hands)
                if (s.Held != null) { held = s; break; }
            if (_guide != null) _guide.Show(held?.Held, held != null ? held.Snap : default);
        }

        private void Step(int h, TrackedHands hands, bool off)
        {
            HandState s = _hands[h];
            Vector3 point = default;
            Pose wrist = default;
            bool pinching = false;
            float gap = float.PositiveInfinity;
            Ray ray = default;
            bool tracked = !off && hands != null &&
                           hands.TryPinch(h, out point, out _, out pinching, out _, out gap) &&
                           hands.TryHandPose(h, out wrist);
            bool hasRay = tracked && hands.TryPointer(h, out ray);
            double now = Time.realtimeSinceStartupAsDouble;

            if (s.Held != null)
            {
                if (off || !s.Held.isActiveAndEnabled)
                    End(s, off ? "a song started or the recenter prompt came up" : "the panel closed", false);
                else
                {
                    s.Trail.Add(now, s.Held.Current);
                    switch (s.Hold.Update(tracked, gap, now, Time.unscaledDeltaTime))
                    {
                        case PinchHold.Step.Hold:
                            if (s.LostFor > 0f) Debug.Log($"[Panels] hand {h} back after {s.LostFor:0.00} s; held still meanwhile");
                            s.LostFor = 0f;
                            Move(s, wrist, ray, hasRay, point);
                            break;
                        case PinchHold.Step.Lost:
                            if (s.LostFor == 0f) s.Losses++;
                            s.LostFor += Time.unscaledDeltaTime;
                            break;
                        case PinchHold.Step.End:
                            End(s, s.Hold.EndedLost
                                ? $"hand tracking lost for {PinchHold.LostSeconds:0.0} s"
                                : $"fingers opened ({gap * 1000f:0} mm apart; {s.Hold.Closest * 1000f:0} mm at their closest)",
                                !s.Hold.EndedLost);
                            break;
                    }
                }
                SetHover(s, null);
                DrawRay(s, false, ray, 0f, s.Held != null && s.Far && hasRay);
                return;
            }

            // What the hand is on.
            GrabbablePanel near = null, far = null;
            GlassButton button = null;
            float farDistance = 0f;
            if (tracked)
            {
                near = Nearest(point, out bool close);
                // A hand right at a panel pokes or grabs it; the ray is for out of reach.
                if (!close && hasRay) far = Pointed(ray, out farDistance, out button);
            }

            if (s.Pinch.Update(tracked, pinching, near != null || far != null, Time.unscaledDeltaTime) == PinchGrab.Step.Start)
            {
                if (button != null)
                {
                    s.Pinch.Release();
                    button.Press();
                    Debug.Log($"[Hands] pinched '{button.name}' from afar");
                }
                else
                {
                    Begin(s, near ?? far, near == null, wrist, ray, point);
                    s.Hold.Begin(gap, now);
                }
            }

            SetHover(s, s.Held == null ? button : null);
            DrawRay(s, hasRay && s.Held == null && far != null, ray, farDistance, false);
        }

        /// <summary>The right controller: its ray on a panel's glass, the
        /// trigger held moves it like a far pinch.</summary>
        private void StepController(bool off)
        {
            HandState s = _hands[ControllerSlot];
            bool tracked = OvrUiPointer.TryControllerRay(out Ray ray, out bool trigger, out bool down, out bool onButton) && !off;

            if (s.Held != null)
            {
                if (!tracked || !trigger || !s.Held.isActiveAndEnabled)
                    End(s, !tracked ? (off ? "a song started" : "the controller was put down")
                                    : !trigger ? "trigger let go" : "the panel closed", false);
                else Move(s, new Pose(ray.origin, Quaternion.identity), ray, true, ray.origin);
            }
            else if (tracked && !onButton && TriggerFree)
            {
                GrabbablePanel panel = Pointed(ray, out float d, out GlassButton b);
                if (panel != null && b == null)
                {
                    ControllerRayEnd = ray.GetPoint(d);
                    if (down) Begin(s, panel, true, new Pose(ray.origin, Quaternion.identity), ray, ray.origin);
                }
                else panel = null;
                ControllerOnPanel = panel != null;
                return;
            }
            ControllerOnPanel = s.Held != null;
            if (s.Held != null) ControllerRayEnd = s.Held.Current.position;
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

        /// <summary>What a held panel follows: the wrist up close, the ray from afar.</summary>
        private static Pose HandPose(HandState s, Pose wrist, Ray ray) =>
            s.Far ? PanelPose.RayPose(ray.origin, ray.direction) : wrist;

        private float Reach(Vector3 point) => _head != null ? Vector3.Distance(_head.position, point) : 0f;

        private void Begin(HandState s, GrabbablePanel panel, bool far, Pose wrist, Ray ray, Vector3 reachFrom)
        {
            s.Held = panel;
            s.Far = far;
            s.PanelInHand = PanelPose.ToFrame(HandPose(s, wrist, ray), panel.Current);
            s.StartReach = Reach(reachFrom);
            s.HeldSince = Time.realtimeSinceStartup;
            s.Snap = default;
            s.LostFor = 0f;
            s.Losses = 0;
            s.Trail.Clear();
            panel.BeginHold(far ? FarRate : NearRate);
            string how = s == _hands[ControllerSlot] ? "with the controller" : far ? "from afar" : "by hand";
            Debug.Log($"[Panels] picked up '{panel.name}' {how}");
        }

        private void Move(HandState s, Pose wrist, Ray ray, bool hasRay, Vector3 reachFrom)
        {
            if (s.Far && !hasRay) return; // keep it where it is until the ray is back
            Pose target = PanelPose.FromFrame(HandPose(s, wrist, ray), s.PanelInHand);
            if (s.Far)
            {
                // Pulling the hand in brings the panel in, faster than the hand moves.
                float pull = (Reach(reachFrom) - s.StartReach) * PullGain;
                Vector3 along = target.position - ray.origin;
                float length = Mathf.Max(ClosestPull, along.magnitude + pull);
                target.position = ray.origin + along.normalized * length;
            }
            // Always upright (no sideways lean), and easy to set straight.
            Vector3? straight = s.Held.TryKeyboard(out Pose keys) ? keys.rotation * Vector3.back : (Vector3?)null;
            PanelPose.Snap was = s.Snap;
            target.rotation = PanelPose.Upright(target.rotation, Vector3.up, straight, ref s.Snap);
            if (s.Snap.Upright != was.Upright || s.Snap.Square != was.Square)
                Debug.Log($"[Panels] '{s.Held.name}' snapped: upright {(s.Snap.Upright ? "on" : "off")}, " +
                          $"square to the keys {(s.Snap.Square ? "on" : "off")}");
            s.Held.HoldAt(target);
        }

        /// <param name="rewind">The fingers opened: put the panel back where it
        /// was the moment they began to.</param>
        private static void End(HandState s, string why, bool rewind)
        {
            if (s.Held == null) return;
            GrabbablePanel panel = s.Held;
            s.Held = null;
            s.Snap = default;
            s.Pinch.Release();
            string back = "";
            Pose? at = null;
            if (rewind && s.Trail.TryAt(s.Hold.ClosedAt, Time.realtimeSinceStartupAsDouble, out Pose then))
            {
                at = then;
                back = $"; put back {Vector3.Distance(then.position, panel.Current.position) * 1000f:0} mm to where it " +
                       $"was {Time.realtimeSinceStartupAsDouble - s.Hold.ClosedAt:0.00} s before";
            }
            string lost = s.Losses > 0 ? $"; hand tracking dropped out {s.Losses} time(s), held still each time" : "";
            Debug.Log($"[Panels] let go of '{panel.name}' after {Time.realtimeSinceStartup - s.HeldSince:0.0} s: {why}{back}{lost}");
            s.Trail.Clear();
            panel.EndHold(at);
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
