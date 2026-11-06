using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// The keyboard stored relative to something that stays put in the room
    /// (a spatial anchor) instead of relative to tracking space, which the
    /// runtime re-bases on every recenter.
    ///
    /// Front-edge points and the toward-player direction are stored in the
    /// anchor's local frame. Rebuilding the layout is then "anchor pose ×
    /// local geometry", so wherever the runtime re-localizes the anchor, the
    /// keyboard follows it -- the project never needs to know a recenter
    /// happened.
    ///
    /// Pure maths, no Meta types, so it compiles into Piano.Core and the
    /// EditMode tests can reach it.
    /// </summary>
    public static class AnchoredKeyboard
    {
        /// <summary>White-key spacing outside this range means the wrong keys
        /// were touched. Same band PianoKeyLayout warns about.</summary>
        public const float MinSpacing = 0.019f;
        public const float MaxSpacing = 0.028f;

        /// <summary>A two-point correction must span at least two octaves:
        /// closer than that, a millimetre of touch error is a large fraction
        /// of the span and the derived spacing and yaw get noisy.</summary>
        public const int MinWhiteKeysBetweenForTwoPoint = 14;

        // ------------------------------------------------------------------
        // Frame
        // ------------------------------------------------------------------

        /// <summary>
        /// Where the anchor goes: the midpoint of the two front-edge points,
        /// rotated about the vertical only. Local +Y is world up and local -Z
        /// points at the player, so local +X is the player's right -- low to
        /// high on any keyboard captured from the front.
        ///
        /// Centring it halves the lever arm that turns a small anchor yaw
        /// error into error at the ends of the keyboard. Keeping it yaw-only
        /// means "vertical" in the stored geometry is always world vertical.
        /// </summary>
        public static Pose Frame(Vector3 lowFront, Vector3 highFront, Vector3 towardPlayer)
        {
            Vector3 right = Horizontal(highFront - lowFront);
            if (right.sqrMagnitude < 1e-10f) right = Vector3.right;
            right.Normalize();

            Vector3 toward = Horizontal(towardPlayer);
            toward -= right * Vector3.Dot(toward, right);
            if (toward.sqrMagnitude < 1e-10f) toward = Vector3.Cross(right, Vector3.up);
            toward.Normalize();

            return new Pose((lowFront + highFront) * 0.5f, Quaternion.LookRotation(-toward, Vector3.up));
        }

        public static Vector3 ToLocal(Pose frame, Vector3 worldPoint) =>
            Quaternion.Inverse(frame.rotation) * (worldPoint - frame.position);

        public static Vector3 ToWorld(Pose frame, Vector3 localPoint) =>
            frame.position + frame.rotation * localPoint;

        public static Vector3 DirectionToLocal(Pose frame, Vector3 worldDirection) =>
            Quaternion.Inverse(frame.rotation) * worldDirection;

        public static Vector3 DirectionToWorld(Pose frame, Vector3 localDirection) =>
            frame.rotation * localDirection;

        /// <summary>World-space keyboard geometry expressed in an anchor's frame.</summary>
        public static KeyboardGeometry ToLocal(Pose anchor, Vector3 lowFront, Vector3 highFront, Vector3 towardPlayer) =>
            new KeyboardGeometry(
                ToLocal(anchor, lowFront),
                ToLocal(anchor, highFront),
                DirectionToLocal(anchor, Horizontal(towardPlayer).normalized));

        /// <summary>
        /// The world layout for an anchor's current pose. The nudge is in the
        /// keyboard's own frame -- millimetres along (right, up, toward
        /// player) -- so it survives a heading change instead of pointing a
        /// different way after one.
        /// </summary>
        public static PianoKeyLayout BuildLayout(Pose anchor, KeyboardGeometry local,
                                                 int lowestNote, int highestNote, float whiteKeyDepth,
                                                 Vector3 nudgeKeyboardMm)
        {
            Vector3 low = ToWorld(anchor, local.LowFront);
            Vector3 high = ToWorld(anchor, local.HighFront);
            Vector3 toward = DirectionToWorld(anchor, local.TowardPlayer);

            Axes(low, high, toward, out Vector3 right, out Vector3 up, out Vector3 forward);
            Vector3 nudge = NudgeToWorld(nudgeKeyboardMm, right, up, forward);

            return PianoKeyLayout.WithFacing(low + nudge, high + nudge, lowestNote, highestNote, toward, whiteKeyDepth);
        }

        /// <summary>The (right, up, forward) basis PianoKeyLayout derives from
        /// these points, without building 88 keys to get it.</summary>
        public static void Axes(Vector3 lowFront, Vector3 highFront, Vector3 towardPlayer,
                                out Vector3 right, out Vector3 up, out Vector3 forward)
        {
            right = (highFront - lowFront).normalized;
            up = (Vector3.up - right * Vector3.Dot(Vector3.up, right)).normalized;
            forward = Vector3.Cross(right, up).normalized;
            if (Vector3.Dot(forward, towardPlayer) < 0f) forward = -forward;
        }

        // ------------------------------------------------------------------
        // Nudge in the keyboard frame
        // ------------------------------------------------------------------

        /// <summary>Millimetres along (right, up, forward) to a world vector in metres.</summary>
        public static Vector3 NudgeToWorld(Vector3 nudgeMm, Vector3 right, Vector3 up, Vector3 forward) =>
            (right * nudgeMm.x + up * nudgeMm.y + forward * nudgeMm.z) * 0.001f;

        /// <summary>A world vector in metres to millimetres along (right, up,
        /// forward). The same projection KeyboardCalibration.NudgeMillimetres
        /// has always reported.</summary>
        public static Vector3 NudgeToKeyboardMm(Vector3 worldNudge, Vector3 right, Vector3 up, Vector3 forward) =>
            new Vector3(Vector3.Dot(worldNudge, right),
                        Vector3.Dot(worldNudge, up),
                        Vector3.Dot(worldNudge, forward)) * 1000f;

        // ------------------------------------------------------------------
        // Yaw fine-adjust
        // ------------------------------------------------------------------

        /// <summary>
        /// Rotate both front-edge points and the facing direction about the
        /// vertical axis through the keyboard's centre. Vertical only: a
        /// keyboard on a stand does not roll, and a tilt here would be a new
        /// error rather than a correction. Works in world or anchor-local
        /// coordinates alike, since a yaw-only anchor shares world up.
        /// </summary>
        public static KeyboardGeometry RotateYaw(KeyboardGeometry g, float degrees)
        {
            Vector3 centre = (g.LowFront + g.HighFront) * 0.5f;
            Quaternion q = Quaternion.AngleAxis(degrees, Vector3.up);
            return new KeyboardGeometry(
                centre + q * (g.LowFront - centre),
                centre + q * (g.HighFront - centre),
                q * g.TowardPlayer);
        }

        /// <summary>Signed yaw, in degrees about world up, that takes
        /// direction <paramref name="from"/> onto <paramref name="to"/>
        /// once both are flattened to horizontal.</summary>
        public static float YawBetween(Vector3 from, Vector3 to) =>
            Vector3.SignedAngle(Horizontal(from), Horizontal(to), Vector3.up);

        // ------------------------------------------------------------------
        // Corrections
        // ------------------------------------------------------------------

        public const int MiddleC = 60;
        /// <summary>One-point correction limits: the vertical part is capped
        /// at this, and anything further than MovedMetres is refused. 150 mm,
        /// up from 50, because the headset itself can place an anchor 86 mm off
        /// after a day away from a piano that never moved (2026-11-05), and one
        /// touch is a fair price for that where a full recalibration is not.
        /// A touch on the wrong key shows at once, and Verifying follows.</summary>
        public const float VerticalCapMetres = 0.01f;
        public const float MovedMetres = 0.15f;

        /// <summary>A touch cannot measure finer than this along the keys and in
        /// depth: fresh calibrations and keys that looked perfect read up to about
        /// 5 mm. Closer than this, the keys are left alone rather than moved by
        /// the touch's own error (2026-11-05, a perfect piano made worse).</summary>
        public const float AlreadyRightMetres = 0.006f;

        /// <summary>A Middle C touch reads 5-10 mm low whatever the keys are doing
        /// (-4.8, -9.4, -7.1 and -6.8 mm, 2026-11-05), so a height this small says
        /// nothing and is not applied.</summary>
        public const float HeightIgnoredMetres = 0.012f;

        /// <summary>
        /// One-point (Middle C) correction. <paramref name="expected"/> is the
        /// front-edge centre the current layout says the key has;
        /// <paramref name="touched"/> is where the controller tip actually is.
        /// The vertical part is capped, because a front-edge touch is much
        /// less reliable vertically than horizontally. Anything over
        /// <paramref name="movedThreshold"/> is rejected: that is not a
        /// calibration error, the piano has moved.
        /// </summary>
        public static OnePointCorrection CorrectOnePoint(Vector3 expected, Vector3 touched,
                                                         float verticalCap = VerticalCapMetres,
                                                         float movedThreshold = MovedMetres)
        {
            Vector3 delta = touched - expected;
            if (delta.magnitude > movedThreshold)
                return OnePointCorrection.Reject(delta,
                    $"Middle C is {delta.magnitude * 1000f:0} mm off -- piano may have moved -- press B to recalibrate");

            delta.y = Mathf.Abs(delta.y) <= HeightIgnoredMetres ? 0f : Mathf.Clamp(delta.y, -verticalCap, verticalCap);
            return OnePointCorrection.Accept(delta);
        }

        /// <summary>Rigid shift of the stored geometry by a world-space delta,
        /// expressed in the anchor's frame. Span and yaw are untouched.</summary>
        public static KeyboardGeometry Translate(KeyboardGeometry g, Vector3 localDelta) =>
            new KeyboardGeometry(g.LowFront + localDelta, g.HighFront + localDelta, g.TowardPlayer);

        /// <summary>
        /// Two-point correction: from two touched white keys with known
        /// notes, solve the front edges of the lowest and highest keys. That
        /// fixes position, yaw and width at once. Full calibration is the
        /// same maths with the two end keys, so one function serves both --
        /// and with n1 = lowest, n2 = highest it returns the touched points
        /// unchanged.
        /// </summary>
        public static TwoPointSolution SolveTwoPoint(int note1, Vector3 point1, int note2, Vector3 point2,
                                                     int lowestNote, int highestNote,
                                                     int minWhiteKeysBetween = MinWhiteKeysBetweenForTwoPoint)
        {
            int lowest = PianoKeyLayout.SnapToWhite(lowestNote, up: true);
            int highest = PianoKeyLayout.SnapToWhite(highestNote, up: false);

            if (note1 == note2)
                return TwoPointSolution.Reject("Those were the same key. Touch two different keys.");
            if (PianoKeyLayout.IsBlackKey(note1) || PianoKeyLayout.IsBlackKey(note2))
                return TwoPointSolution.Reject("Use white keys for a two-point correction.");
            if (note1 < lowest || note2 < lowest || note1 > highest || note2 > highest)
                return TwoPointSolution.Reject("One of those keys is outside this keyboard's range.");

            if (note2 < note1)
            {
                (note1, note2) = (note2, note1);
                (point1, point2) = (point2, point1);
            }

            int whitesBetween = PianoKeyLayout.CountWhiteKeysBelow(note1, note2);
            if (whitesBetween < minWhiteKeysBetween)
                return TwoPointSolution.Reject(
                    $"Those keys are only {whitesBetween} white keys apart. Use two at least two octaves apart.");

            Vector3 span = point2 - point1;
            float distance = span.magnitude;
            if (distance < 1e-4f)
                return TwoPointSolution.Reject("Both touches landed on the same spot.");

            float spacing = distance / whitesBetween;
            if (spacing < MinSpacing || spacing > MaxSpacing)
                return TwoPointSolution.Reject(
                    $"That gives a white key {spacing * 1000f:0.0} mm wide (expected about 23.5). " +
                    "Were those the prompted keys?");

            Vector3 right = span / distance;
            float alongToNote1 = PianoKeyLayout.CountWhiteKeysBelow(lowest, note1) * spacing;
            float fullSpan = (PianoKeyLayout.CountWhiteKeys(lowest, highest) - 1) * spacing;

            Vector3 lowFront = point1 - right * alongToNote1;
            return TwoPointSolution.Accept(lowFront, lowFront + right * fullSpan, spacing);
        }

        // ------------------------------------------------------------------
        // Plausibility
        // ------------------------------------------------------------------

        /// <summary>Null when the stored geometry could be a real keyboard;
        /// otherwise the reason it cannot. These are the checks report
        /// section 9 applies before trusting a saved profile.</summary>
        public static string CheckPlausible(KeyboardGeometry g, int lowestNote, int highestNote)
        {
            if (!IsFinite(g.LowFront) || !IsFinite(g.HighFront) || !IsFinite(g.TowardPlayer))
                return "geometry contains a non-finite number";

            if (lowestNote < 21 || highestNote > 108 || lowestNote >= highestNote)
                return $"note range {lowestNote}-{highestNote} is outside 21-108 or empty";

            float span = Vector3.Distance(g.LowFront, g.HighFront);
            if (span < 0.3f || span > 1.6f)
                return $"span {span:0.000} m is outside 0.3-1.6 m";

            int whites = PianoKeyLayout.CountWhiteKeys(
                PianoKeyLayout.SnapToWhite(lowestNote, up: true),
                PianoKeyLayout.SnapToWhite(highestNote, up: false));
            float spacing = whites > 1 ? span / (whites - 1) : 0f;
            if (spacing < MinSpacing || spacing > MaxSpacing)
                return $"white-key spacing {spacing * 1000f:0.0} mm is outside 19-28 mm";

            float length = g.TowardPlayer.magnitude;
            if (Mathf.Abs(length - 1f) > 0.01f)
                return $"toward-player direction has length {length:0.000}, not 1";

            float tilt = Mathf.Abs(90f - Vector3.Angle(g.TowardPlayer, Vector3.up));
            if (tilt > 5f)
                return $"toward-player direction is {tilt:0.0} degrees off horizontal";

            return null;
        }

        // ------------------------------------------------------------------
        // Following a live anchor
        // ------------------------------------------------------------------

        /// <summary>Below this the anchor has not really moved: the runtime's
        /// pose jitters by fractions of a millimetre frame to frame, and
        /// rebuilding 88 keys for that is wasted work.</summary>
        public const float RebuildMetres = 0.001f;
        public const float RebuildDegrees = 0.1f;

        /// <summary>True when <paramref name="now"/> is more than
        /// <paramref name="metres"/> or <paramref name="degrees"/> away from
        /// <paramref name="last"/> -- the pose the current layout was built
        /// from. Compared against that pose, not last frame's, so slow drift
        /// accumulates until it crosses the threshold instead of hiding
        /// under it forever.</summary>
        public static bool PoseMoved(Pose last, Pose now, float metres = RebuildMetres, float degrees = RebuildDegrees) =>
            (now.position - last.position).sqrMagnitude > metres * metres ||
            Quaternion.Angle(last.rotation, now.rotation) > degrees;

        // ------------------------------------------------------------------

        public static Vector3 Horizontal(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    /// <summary>Front-edge points and facing, in whatever frame the caller
    /// is working in -- anchor-local for storage, world for display.</summary>
    public readonly struct KeyboardGeometry
    {
        public readonly Vector3 LowFront;
        public readonly Vector3 HighFront;
        public readonly Vector3 TowardPlayer;

        public KeyboardGeometry(Vector3 lowFront, Vector3 highFront, Vector3 towardPlayer)
        {
            LowFront = lowFront; HighFront = highFront; TowardPlayer = towardPlayer;
        }
    }

    public readonly struct OnePointCorrection
    {
        public readonly bool Accepted;
        /// <summary>World-space shift to apply, vertical part already capped.
        /// For a rejection, the raw measured offset, for logging.</summary>
        public readonly Vector3 Delta;
        public readonly string Reason;

        private OnePointCorrection(bool accepted, Vector3 delta, string reason)
        {
            Accepted = accepted; Delta = delta; Reason = reason;
        }

        /// <summary>Accepted, and too small to tell from the touch's own error.</summary>
        public bool AlreadyRight =>
            Accepted && Delta.y == 0f && new Vector2(Delta.x, Delta.z).magnitude <= AnchoredKeyboard.AlreadyRightMetres;

        public static OnePointCorrection Accept(Vector3 delta) => new OnePointCorrection(true, delta, null);
        public static OnePointCorrection Reject(Vector3 delta, string reason) => new OnePointCorrection(false, delta, reason);
    }

    public readonly struct TwoPointSolution
    {
        public readonly bool Accepted;
        public readonly Vector3 LowFront;
        public readonly Vector3 HighFront;
        public readonly float WhiteKeySpacing;
        public readonly string Reason;

        private TwoPointSolution(bool accepted, Vector3 low, Vector3 high, float spacing, string reason)
        {
            Accepted = accepted; LowFront = low; HighFront = high; WhiteKeySpacing = spacing; Reason = reason;
        }

        public static TwoPointSolution Accept(Vector3 low, Vector3 high, float spacing) =>
            new TwoPointSolution(true, low, high, spacing, null);

        public static TwoPointSolution Reject(string reason) =>
            new TwoPointSolution(false, default, default, 0f, reason);
    }
}
