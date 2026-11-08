using UnityEngine;

namespace PianoGlass.Song
{
    /// <summary>
    /// Every note colour in one asset, Assets/UI/NotePalette.asset. Nothing
    /// else hard-codes these, so colour themes (#10) only have to change this.
    ///
    /// The hit, missed and wrong colours are reserved: a hand colour that
    /// looked like one of them would make every note look judged. An EditMode
    /// test holds every palette to <see cref="MinimumDistance"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "PianoGlass/Note Palette", fileName = "NotePalette")]
    public sealed class NotePalette : ScriptableObject
    {
        public static readonly Color DefaultLeftHand = FromHex(0xE8, 0xB8, 0x6D);  // sand     #E8B86D
        public static readonly Color DefaultRightHand = FromHex(0x66, 0xAD, 0xF2); // blue     #66ADF2
        public static readonly Color DefaultAllNotes = FromHex(0xB4, 0x9C, 0xF0);  // lavender #B49CF0
        public static readonly Color DefaultHit = FromHex(0x3D, 0xDC, 0x97);       // mint     #3DDC97
        public static readonly Color DefaultMissed = FromHex(0x5A, 0x63, 0x6E);    // slate    #5A636E
        public static readonly Color DefaultWrong = FromHex(0xFF, 0x6B, 0x5B);     // coral    #FF6B5B

        /// <summary>How far apart two colours must be, as straight-line
        /// distance through red, green and blue from 0 to 1.</summary>
        public const float MinimumDistance = 0.25f;

        [Header("Hands")]
        public Color leftHand = DefaultLeftHand;
        public Color rightHand = DefaultRightHand;
        [Tooltip("Songs whose hands aren't known. Doesn't hint at either hand.")]
        public Color allNotes = DefaultAllNotes;

        [Header("Outcomes (reserved)")]
        public Color hit = DefaultHit;
        public Color missed = DefaultMissed;
        public Color wrong = DefaultWrong;

        /// <summary>The palette in use. Set once when the app starts; the
        /// defaults stand in until then and in tests.</summary>
        public static NotePalette Active { get; set; }

        /// <summary>The colour of the notes being practiced.</summary>
        public Color SolidColor(PracticeHand hand) =>
            hand == PracticeHand.Left ? leftHand : hand == PracticeHand.Right ? rightHand : allNotes;

        /// <summary>The colour of an outline note, which is the other hand's.</summary>
        public Color OutlineColor(NoteHand hand) => hand == NoteHand.Left ? leftHand : rightHand;

        public static float Distance(Color a, Color b)
        {
            float r = a.r - b.r, g = a.g - b.g, bl = a.b - b.b;
            return Mathf.Sqrt(r * r + g * g + bl * bl);
        }

        /// <summary>The first two colours that are too close to tell apart,
        /// or null if every hand colour is clear of the others and of every
        /// outcome colour.</summary>
        public string FindClash()
        {
            (string name, Color color)[] hands =
                { ("left hand", leftHand), ("right hand", rightHand), ("all notes", allNotes) };
            (string name, Color color)[] outcomes = { ("hit", hit), ("missed", missed), ("wrong", wrong) };

            for (int i = 0; i < hands.Length; i++)
            {
                for (int j = i + 1; j < hands.Length; j++)
                    if (Distance(hands[i].color, hands[j].color) < MinimumDistance)
                        return $"{hands[i].name} is too close to {hands[j].name}";
                foreach ((string name, Color color) outcome in outcomes)
                    if (Distance(hands[i].color, outcome.color) < MinimumDistance)
                        return $"{hands[i].name} is too close to {outcome.name}";
            }
            return null;
        }

        private static Color FromHex(byte r, byte g, byte b) => new Color32(r, g, b, 255);
    }
}
