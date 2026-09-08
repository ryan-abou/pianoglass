using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    public readonly struct PianoKey
    {
        public readonly int Note;
        public readonly Vector3 Center;      // world space, middle of the key's playing surface
        public readonly Quaternion Rotation; // faces up, +Z along the keyboard's depth
        public readonly float Width;
        public readonly float Depth;
        public readonly bool IsBlack;

        public PianoKey(int note, Vector3 center, Quaternion rotation, float width, float depth, bool isBlack)
        {
            Note = note; Center = center; Rotation = rotation;
            Width = width; Depth = depth; IsBlack = isBlack;
        }
    }

    /// <summary>
    /// Turns two measured points into the position of every key on the keyboard.
    ///
    /// The two points are the CENTRE OF THE FRONT EDGE of the lowest key and of
    /// the highest key — the edge nearest you. That edge is chosen because it is
    /// the easiest place on a piano to touch accurately with a controller tip:
    /// nothing is in the way and you can feel when you have arrived.
    ///
    /// Everything else is derived. No per-key measurement, no scanning, no
    /// machine learning. Two points and the layout of a piano, which has not
    /// changed since 1700.
    /// </summary>
    public class PianoKeyLayout
    {
        // A full-size key is 23.5 mm wide at the front. Anything far from this
        // means the calibration points were wrong, which is worth shouting about.
        public const float StandardWhiteKeySpacing = 0.0235f;
        private const float MinPlausibleSpacing = 0.019f;
        private const float MaxPlausibleSpacing = 0.028f;

        public int LowestNote { get; }
        public int HighestNote { get; }
        public float WhiteKeySpacing { get; }
        public float WhiteKeyDepth { get; }
        public Vector3 Right { get; }     // unit vector toward higher notes
        public Vector3 Up { get; }        // out of the key surface
        public Vector3 Forward { get; }   // toward the player
        public Quaternion Rotation { get; }

        /// <summary>Non-null when the derived key width looks implausible.</summary>
        public string Warning { get; }

        private readonly Vector3 _lowFrontEdge;
        private readonly Dictionary<int, PianoKey> _keys = new Dictionary<int, PianoKey>();

        public IReadOnlyDictionary<int, PianoKey> Keys => _keys;
        public int KeyCount => _keys.Count;

        /// <summary>Black key nudge, in fractions of a white key width, indexed by
        /// pitch class. Real acoustic pianos are not perfectly centred; leave these
        /// at zero until you can see the misalignment on your own keyboard.</summary>
        public static readonly float[] BlackKeyNudge = new float[12];

        public PianoKeyLayout(
            Vector3 lowestKeyFrontEdge,
            Vector3 highestKeyFrontEdge,
            int lowestNote,
            int highestNote,
            Vector3 playerPosition,
            float whiteKeyDepth = 0.145f)
        {
            LowestNote = SnapToWhite(lowestNote, up: true);
            HighestNote = SnapToWhite(highestNote, up: false);
            WhiteKeyDepth = whiteKeyDepth;
            _lowFrontEdge = lowestKeyFrontEdge;

            Vector3 span = highestKeyFrontEdge - lowestKeyFrontEdge;
            Right = span.normalized;

            // Keyboards sit on flat surfaces, so world up is a good enough normal.
            // Orthogonalise it against Right so the basis stays square even if the
            // two captured points are at slightly different heights.
            Up = (Vector3.up - Right * Vector3.Dot(Vector3.up, Right)).normalized;
            Forward = Vector3.Cross(Right, Up).normalized;

            // Forward must point at the player, not away. Rather than making the
            // user tell us, look at where their head is.
            if (Vector3.Dot(Forward, playerPosition - lowestKeyFrontEdge) < 0f)
                Forward = -Forward;

            Rotation = Quaternion.LookRotation(Forward, Up);

            int whiteCount = CountWhiteKeys(LowestNote, HighestNote);
            WhiteKeySpacing = whiteCount > 1 ? span.magnitude / (whiteCount - 1) : StandardWhiteKeySpacing;

            if (WhiteKeySpacing < MinPlausibleSpacing || WhiteKeySpacing > MaxPlausibleSpacing)
            {
                Warning =
                    $"Derived white key width is {WhiteKeySpacing * 1000f:0.0} mm " +
                    $"(expected about {StandardWhiteKeySpacing * 1000f:0.0} mm). " +
                    $"Either the two points were not the lowest and highest keys, or " +
                    $"the note range {LowestNote}-{HighestNote} does not match this keyboard.";
            }

            Build();
        }

        private void Build()
        {
            _keys.Clear();

            float blackDepth = WhiteKeyDepth * 0.62f;
            float blackWidth = WhiteKeySpacing * 0.55f;
            float blackRise = 0.010f;   // black keys stand about 1 cm proud

            for (int note = LowestNote; note <= HighestNote; note++)
            {
                bool black = IsBlackKey(note);
                float along;

                if (black)
                {
                    // A black key sits between the white key below and the white
                    // key above it: exactly half a white key back from the count
                    // of white keys beneath it.
                    along = (CountWhiteKeysBelow(LowestNote, note) - 0.5f) * WhiteKeySpacing;
                    along += BlackKeyNudge[note % 12] * WhiteKeySpacing;
                }
                else
                {
                    along = CountWhiteKeysBelow(LowestNote, note) * WhiteKeySpacing;
                }

                float depth = black ? blackDepth : WhiteKeyDepth;

                // Captured points are front edges, so step back by half a key to
                // reach the centre of the playing surface. Black keys are aligned
                // to the BACK of the white keys, so they step back further.
                float backOffset = black
                    ? WhiteKeyDepth - blackDepth * 0.5f
                    : WhiteKeyDepth * 0.5f;

                Vector3 center = _lowFrontEdge
                                 + Right * along
                                 - Forward * backOffset
                                 + Up * (black ? blackRise : 0f);

                _keys[note] = new PianoKey(
                    note,
                    center,
                    Rotation,
                    black ? blackWidth : WhiteKeySpacing * 0.94f,
                    depth,
                    black);
            }
        }

        public bool TryGetKey(int note, out PianoKey key) => _keys.TryGetValue(note, out key);

        public bool Contains(int note) => _keys.ContainsKey(note);

        // ------------------------------------------------------------------
        // Piano arithmetic
        // ------------------------------------------------------------------

        public static bool IsBlackKey(int note)
        {
            int pc = ((note % 12) + 12) % 12;
            return pc == 1 || pc == 3 || pc == 6 || pc == 8 || pc == 10;
        }

        /// <summary>White keys in the half-open range [from, note).</summary>
        public static int CountWhiteKeysBelow(int from, int note)
        {
            int n = 0;
            for (int i = from; i < note; i++) if (!IsBlackKey(i)) n++;
            return n;
        }

        /// <summary>White keys in the closed range [from, to].</summary>
        public static int CountWhiteKeys(int from, int to)
        {
            int n = 0;
            for (int i = from; i <= to; i++) if (!IsBlackKey(i)) n++;
            return n;
        }

        /// <summary>The physical end keys of a piano are always white, so nudge
        /// a mistyped range onto the nearest white key rather than failing.</summary>
        public static int SnapToWhite(int note, bool up)
        {
            int guard = 0;
            while (IsBlackKey(note) && guard++ < 3) note += up ? 1 : -1;
            return note;
        }

        /// <summary>Common keyboard sizes, for the calibration inspector.</summary>
        public static (int low, int high) RangeForKeyCount(int keys) => keys switch
        {
            88 => (21, 108),   // A0  - C8
            76 => (28, 103),   // E1  - G7
            73 => (28, 100),   // E1  - E7
            61 => (36, 96),    // C2  - C7
            49 => (36, 84),    // C2  - C6
            37 => (48, 84),    // C3  - C6
            25 => (48, 72),    // C3  - C5
            _  => (36, 96)
        };
    }
}
