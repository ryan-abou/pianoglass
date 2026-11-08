using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.Scoring;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.Highway
{
    /// <summary>
    /// The falling-note highway: one thin vertical slab per note, standing up
    /// from the far edge of its key (the edge away from the player) and
    /// sliding down into the key line as SongClock advances. This is the
    /// Synthesia arrangement -- a wall behind the keys, not a tile floating
    /// above them -- because a horizontal tile seen from a seated headset
    /// viewpoint is nearly edge-on, and "how far above the key is it" is the
    /// hardest depth judgement a Quest 2 can ask of someone. Since Phase 1
    /// the wall leans back (tiltDegrees &lt; 0, set by SceneSetup) to match
    /// the reference video's runway; it still rises from the keys' far edge
    /// rather than floating over them.
    ///
    /// A slab's height IS its note's remaining duration on screen -- see
    /// SlabGeometry.VerticalExtent, the pure function Update() calls for the
    /// clamp maths. That means localScale is recomputed every frame.
    ///
    /// The slabs are pooled: a small set is reused as notes scroll by, rather
    /// than one object per note in the song, so a long song costs no more
    /// than a short one. A note keeps its slab for as long as it is showing.
    ///
    /// Practicing one hand, your notes are solid and the other hand's are
    /// outlines in their own colour. Outlines have no name, never change
    /// colour and don't fade: memory fade tests your memory of your own part.
    /// Scoring never looks at them, so nothing here has to tell it.
    ///
    /// Thin Cubes, not the Quad primitive: PianoKeyHighlighter hit this exact
    /// choice for the same reason -- a Quad's normal can end up facing away
    /// from the player with nothing in the console to explain why it's
    /// invisible. A thin Cube looks identical to a slab and has no facing to
    /// get backwards.
    ///
    /// Outcome colours (F19/F20): a slab tints mint when its note is hit and
    /// greys to slate when it is missed, fading over OutcomeTint.FadeSeconds.
    /// Driven by ScoreKeeper's events, so judging stays in ScoringSession;
    /// a restart puts every slab back to its base colour.
    ///
    /// Note names (F14): each slab carries its pitch name ("C", "F♯") as a
    /// 3D TextMeshPro label lying on the wall just in front of the slab,
    /// at its bottom end -- the end that meets the key line. NoteLabel
    /// (pure, tested) decides the text and whether the slab is tall enough
    /// to hold it. Each pooled slab has one label.
    ///
    /// Memory fade (F36): as a loop's ladder moves, SongClock.NoteOpacityAt is
    /// below 1. Slabs then draw on the transparent material at that alpha and
    /// their names fade with them; outcome colours keep it (OutcomeTint.FadedSlab).
    /// The opaque material is used whenever nothing is fading, so an ordinary
    /// song costs what it always did. At 0 the note is simply not drawn.
    /// </summary>
    public class NoteHighway : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private SongClock clock;
        [Tooltip("Source of hit/miss judgements for the outcome colours. Optional: " +
                 "without it the slabs simply never change colour.")]
        [SerializeField] private ScoreKeeper scoreKeeper;
        [Tooltip("Which song and hand to draw.")]
        [SerializeField] private CurrentSong current;

        [Header("Appearance")]
        [Tooltip("Assign a material made in the editor -- a runtime Shader.Find " +
                 "can be stripped from a device build.")]
        [SerializeField] private Material noteMaterial;
        [Tooltip("Transparent, for memory fade. Assigned by SceneSetup.")]
        [SerializeField] private Material fadedNoteMaterial;
        [Tooltip("PianoGlass/NoteOutline, for the other hand's notes. Assigned by SceneSetup.")]
        [SerializeField] private Material outlineMaterial;
        [Tooltip("Metres an outline sits behind a solid note on the same key.")]
        [SerializeField] private float outlineBehind = 0.0005f;
        [Tooltip("Thickness of the wall, front to back. It's meant to read as a " +
                 "flat plane, so keep this small.")]
        [SerializeField] private float slabThickness = 0.006f;
        [Range(0.1f, 1f)]
        [Tooltip("Fraction of the key's width a slab fills, so adjacent notes " +
                 "don't touch.")]
        [SerializeField] private float widthFraction = 0.9f;
        [Tooltip("Metres trimmed off the top of every slab, so two of the same " +
                 "note in a row read as two notes, not one long bar. Drawing " +
                 "only: note timing is untouched.")]
        [SerializeField] private float noteGapMetres = 0.008f;

        [Header("Note names (F14)")]
        [SerializeField] private bool showNoteNames = true;
        [Tooltip("Assigned by SceneSetup (Configure Note Names) -- never looked up at runtime.")]
        [SerializeField] private TMP_FontAsset labelFont;
        [Tooltip("The font's SDF material, assigned by SceneSetup.")]
        [SerializeField] private Material labelMaterial;
        [SerializeField] private Color labelColor = new Color(0.04f, 0.07f, 0.12f, 0.92f);
        [Tooltip("TMP font size; auto-sized down from this only where a name is " +
                 "wider than its slab (two-character names on black keys).")]
        [SerializeField] private float labelFontSize = 0.11f;
        [Tooltip("Metres of wall the label takes up, along the wall.")]
        [SerializeField] private float labelHeight = 0.011f;
        [Tooltip("Clear wall above and below the label; a slab shorter than " +
                 "labelHeight + 2 x this shows no name.")]
        [SerializeField] private float labelPadding = 0.003f;

        [Header("Highway")]
        [Tooltip("Seconds of warning before a note is due -- how far up the wall it spawns.")]
        [SerializeField] private float leadTimeSeconds = 3f;
        [Tooltip("Height of the wall, in metres, from the key line to where a note " +
                 "spawns.")]
        [SerializeField] private float highwayHeight = 0.5f;
        [Tooltip("Small standoff behind the key's far edge (away from the player), " +
                 "so the wall doesn't clip into the key body.")]
        [SerializeField] private float depthOffset = 0.010f;
        [Tooltip("Degrees the wall leans, about layout.Right. Positive tips the " +
                 "top toward the player; negative leans it back into a runway, " +
                 "which is what the reference video's highway does; 0 is upright.")]
        [SerializeField] private float tiltDegrees = 0f;

        public KeyboardCalibration Calibration => calibration;
        public SongClock Clock => clock;
        public SongData Song => _song;
        public float LeadTimeSeconds => leadTimeSeconds;
        public float HighwayHeight => highwayHeight;
        public float DepthOffset => depthOffset;

        /// <summary>Metres of wall per second of song time.</summary>
        public float Speed => highwayHeight / Mathf.Max(0.01f, leadTimeSeconds);

        /// <summary>
        /// The wall's up direction (key line to spawn point) and its normal
        /// (toward the player), once tilt is applied. Built from Up and
        /// Forward directly rather than from an AngleAxis about Right, so the
        /// sign of tiltDegrees means the same thing whichever way Right's
        /// handedness came out of calibration. HighwayGrid draws on exactly
        /// this plane, so the grid and the slabs can never disagree.
        /// </summary>
        public (Vector3 up, Vector3 normal) PlaneAxes(PianoKeyLayout layout)
        {
            float rad = tiltDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return (layout.Up * c + layout.Forward * s,
                    layout.Forward * c - layout.Up * s);
        }

        /// <summary>One reusable slab and its name.</summary>
        private sealed class Slab
        {
            public Renderer Renderer;
            public TextMeshPro Label;   // null where labels are off
            public int Note = -1;       // index in _song.Notes, -1 when free
            public int Frame;           // the last frame it was drawn
            public Color Applied;       // so the property block is only touched when it changes
            public float LabelOpacity = 1f;
            public Material Material;
        }

        /// <summary>Made ahead of time, so the first bars don't stutter.
        /// More are made if a song ever needs them.</summary>
        private const int SlabsMadeAhead = 48;

        private SongData _song;
        private PracticeHand _hand;
        private NoteWindow _window;
        private readonly Stack<Slab> _free = new Stack<Slab>();
        private readonly List<Slab> _inUse = new List<Slab>();
        private int _slabsMade;
        private int _frame;
        private bool _labelsChecked, _labelsOn, _sharpGlyph;
        private readonly string[] _labelNames = new string[128];

        // All indexed by the note's position in _song.Notes -- what
        // JudgementResult.noteIndex refers to.
        private Slab[] _slabOfNote = new Slab[0];
        private NoteOutcome[] _outcome = new NoteOutcome[0];
        private float[] _judgedAt = new float[0];
        // What each note last showed while pending, which a judged note may
        // not rise above.
        private float[] _noteCeiling = new float[0];
        private MaterialPropertyBlock _block;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            // FindAnyObjectByType, not FindFirstObjectByType: deprecated in 6.6
            // because it relies on instance-ID ordering, and we only ever
            // expect one of each in the scene anyway.
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (scoreKeeper == null) scoreKeeper = FindAnyObjectByType<ScoreKeeper>();
            if (current == null) current = FindAnyObjectByType<CurrentSong>();
            _block = new MaterialPropertyBlock();
            UseCurrentSong();
        }

        private void UseCurrentSong()
        {
            if (current == null) return;
            _song = current.Song;
            _hand = current.Hand;
            _window = new NoteWindow(_song.Notes);
            int count = _song.Notes.Count;
            _slabOfNote = new Slab[count];
            _outcome = new NoteOutcome[count];
            _judgedAt = new float[count];
            _noteCeiling = new float[count];
            for (int i = 0; i < count; i++) _noteCeiling[i] = 1f;
        }

        private void OnSongChanged()
        {
            ReleaseAll();
            UseCurrentSong();
            MakeSlabsAhead();
        }

        private void OnEnable()
        {
            if (current != null) current.SongChanged += OnSongChanged;
            if (scoreKeeper != null)
            {
                scoreKeeper.NoteJudged += OnNoteJudged;
                scoreKeeper.AttemptReset += ClearOutcomes;
            }
            if (calibration == null) return;
            calibration.Calibrated += OnCalibrated;
            calibration.PlacementValidChanged += OnPlacementValidChanged;
            if (calibration.IsCalibrated) MakeSlabsAhead();
        }

        private void OnDisable()
        {
            if (current != null) current.SongChanged -= OnSongChanged;
            if (scoreKeeper != null)
            {
                scoreKeeper.NoteJudged -= OnNoteJudged;
                scoreKeeper.AttemptReset -= ClearOutcomes;
            }
            if (calibration == null) return;
            calibration.Calibrated -= OnCalibrated;
            calibration.PlacementValidChanged -= OnPlacementValidChanged;
        }

        /// <summary>Update() stops placing slabs while placement is invalid,
        /// but would leave the ones already showing where they were -- so
        /// hide them here, once, at the moment it becomes invalid.</summary>
        private void OnPlacementValidChanged(bool valid)
        {
            if (valid) return; // Update() shows whatever is due on the next frame
            ReleaseAll();
        }

        private void OnNoteJudged(JudgementResult r)
        {
            if (r.noteIndex < 0 || r.noteIndex >= _outcome.Length) return;
            _outcome[r.noteIndex] = r.pressed ? NoteOutcome.Hit : NoteOutcome.Missed;
            _judgedAt[r.noteIndex] = Time.realtimeSinceStartup;
        }

        /// <summary>Restart or a new attempt: every note is pending again.
        /// Update() writes the base colour back on its next pass.</summary>
        private void ClearOutcomes()
        {
            System.Array.Clear(_outcome, 0, _outcome.Length);
        }

        private void OnCalibrated(PianoKeyLayout layout)
        {
            // Update() re-reads calibration.Layout every frame, so the slabs
            // already track a nudge. This only makes them the first time
            // there's something to place: a held nudge stick fires this every
            // frame.
            MakeSlabsAhead();
        }

        // ------------------------------------------------------------------

        private void MakeSlabsAhead()
        {
            if (_song == null) return;
            int wanted = Mathf.Min(_song.Notes.Count, SlabsMadeAhead);
            while (_slabsMade < wanted) _free.Push(NewSlab());
        }

        /// <summary>Whether names can be drawn, asked once. Of the font
        /// itself, fallbacks included, so a font swap that loses the glyph
        /// degrades to "F#" rather than a box.</summary>
        private void CheckLabels()
        {
            if (_labelsChecked) return;
            _labelsChecked = true;

            _labelsOn = showNoteNames && labelFont != null;
            if (showNoteNames && labelFont == null)
                Debug.LogError("[NoteHighway] Note names on but no label font assigned -- " +
                               "run PianoGlass/Scene/Configure Note Names.");
            _sharpGlyph = _labelsOn && labelFont.HasCharacter(NoteLabel.SharpGlyph, true, true);
            if (_labelsOn)
                Debug.Log("[NoteHighway] note names on, sharp drawn as " +
                          (_sharpGlyph ? "U+266F" : "'#' (font has no U+266F)"));
        }

        private Slab NewSlab()
        {
            CheckLabels();

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"NoteSlab_{_slabsMade}";
            go.transform.SetParent(transform, false);

            // Colliders would fight with hand/controller physics for no gain.
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            _slabsMade++;
            return new Slab { Renderer = renderer, Label = _labelsOn ? CreateLabel() : null };
        }

        /// <summary>This note's slab, taking a free one if it has none yet.</summary>
        private Slab SlabFor(int index, SongNote note, float keyWidth, bool named)
        {
            Slab slab = _slabOfNote[index];
            if (slab != null) return slab;

            slab = _free.Count > 0 ? _free.Pop() : NewSlab();
            slab.Note = index;
            _slabOfNote[index] = slab;
            _inUse.Add(slab);
            slab.Renderer.enabled = true;

            if (slab.Label != null && named)
            {
                string name = LabelName(note.Pitch);
                if (slab.Label.text != name) slab.Label.text = name;
                slab.Label.rectTransform.sizeDelta = new Vector2(keyWidth * widthFraction, labelHeight);
            }
            return slab;
        }

        private string LabelName(int pitch)
        {
            if (pitch < 0 || pitch >= _labelNames.Length) return NoteLabel.Name(pitch, _sharpGlyph);
            return _labelNames[pitch] ?? (_labelNames[pitch] = NoteLabel.Name(pitch, _sharpGlyph));
        }

        private void Release(Slab slab)
        {
            if (slab.Note >= 0 && slab.Note < _slabOfNote.Length) _slabOfNote[slab.Note] = null;
            slab.Note = -1;
            slab.Renderer.enabled = false;
            SetLabelVisible(slab.Label, false);
            _free.Push(slab);
        }

        private void ReleaseAll()
        {
            foreach (Slab slab in _inUse) Release(slab);
            _inUse.Clear();
        }

        private void Update()
        {
            if (clock == null || calibration == null || !calibration.IsPlacementValid ||
                !calibration.IsCalibrated || _song == null || _window == null) return;

            PianoKeyLayout layout = calibration.Layout;
            // Where the song will be when this frame reaches the display, not
            // where it is now -- otherwise every note lands on the key line
            // one display latency late, and the player presses late with it.
            double songTime = clock.DisplaySongTime;
            float speed = Speed;

            // Shared by every slab this frame: the wall's orientation and its
            // vertical axis once tilt is applied. Up and the normal rotate
            // together, so the wall stays a flat plane instead of shearing.
            (Vector3 tiltedUp, Vector3 normal) = PlaneAxes(layout);
            Quaternion tiltRotation = Quaternion.LookRotation(normal, tiltedUp);

            float now = Time.realtimeSinceStartup;
            bool fading = clock.IsFading && clock.Fade.ShownStep > 0;
            if (fading && fadedNoteMaterial == null && !_warnedNoFadedMaterial)
            {
                _warnedNoFadedMaterial = true;
                Debug.LogError("[NoteHighway] No faded note material -- run PianoGlass/Scene/Configure Memory Fade.");
            }
            Material solid = fading && fadedNoteMaterial != null ? fadedNoteMaterial : SolidMaterial;
            Color handColor = current.Palette.SolidColor(_hand);

            _frame++;
            _window.Range(songTime, leadTimeSeconds, out int from, out int to);
            for (int i = from; i < to; i++)
            {
                int index = _window.NoteAt(i);
                SongNote note = _song.Notes[index];
                bool mine = note.BelongsTo(_hand);
                if (!mine && outlineMaterial == null) continue;
                if (!layout.TryGetKey(note.Pitch, out PianoKey key)) continue; // outside this keyboard

                (float b, float height) = SlabGeometry.VerticalExtent(
                    note.StartTime, note.EndTime, songTime, speed, highwayHeight, noteGapMetres);

                // SlabGeometry's clamps alone are what cull a note that hasn't
                // arrived yet, or has fully passed, and what make a held
                // note's slab shrink into the key line as it plays rather
                // than sliding underneath it.
                // In a practice loop, only the loop's own notes are drawn, and
                // a fully faded note not at all: being judged does not bring it
                // back, nor does help that arrives after it was judged.
                float opacity = 1f;
                if (mine)
                {
                    opacity = clock.NoteOpacityAt(note.StartTime, leadTimeSeconds);
                    if (_outcome[index] == NoteOutcome.Pending) _noteCeiling[index] = opacity;
                    else opacity = Mathf.Min(opacity, _noteCeiling[index]);
                }

                bool visible = height > 0f && opacity > 0f &&
                               (!(clock.Loop is LoopRange loop) || loop.Holds(note.StartTime));
                if (!visible) continue;

                Slab slab = SlabFor(index, note, key.Width, mine);
                slab.Frame = _frame;
                Material material = mine ? solid : outlineMaterial;
                if (slab.Material != material)
                {
                    slab.Material = material;
                    if (material != null) slab.Renderer.sharedMaterial = material;
                }

                // PianoKey.Center is the middle of the key's playing surface
                // (front to back), and Forward points at the player, so the
                // far edge -- away from the player -- is Center minus half
                // the key's own depth along Forward.
                Vector3 farEdge = key.Center - layout.Forward * (key.Depth * 0.5f + depthOffset);
                Vector3 pos = farEdge + tiltedUp * (b + height * 0.5f) - normal * (mine ? 0f : outlineBehind);
                Transform t = slab.Renderer.transform;
                t.SetPositionAndRotation(pos, tiltRotation);
                t.localScale = new Vector3(key.Width * widthFraction, height, slabThickness);

                TextMeshPro label = slab.Label;
                if (label != null)
                {
                    bool fits = mine && NoteLabel.Fits(height, labelHeight, labelPadding);
                    SetLabelVisible(label, fits);
                    if (fits)
                    {
                        if (!Mathf.Approximately(opacity, slab.LabelOpacity)) SetLabelOpacity(slab, opacity);

                        // Just proud of the slab's front face, so it never
                        // z-fights it. TMP reads correctly with its +z
                        // pointing away from the viewer: -normal, as
                        // HighwayGrid's bar tags do.
                        Vector3 labelPos = farEdge
                            + tiltedUp * (b + NoteLabel.CentreAboveBottom(labelHeight, labelPadding))
                            + normal * (slabThickness * 0.5f + 0.0005f);
                        label.transform.SetPositionAndRotation(labelPos, Quaternion.LookRotation(-normal, tiltedUp));
                    }
                }

                Color color = mine
                    ? OutcomeTint.FadedSlab(handColor, _outcome[index], now - _judgedAt[index],
                                            OutcomeTint.Hit, OutcomeTint.Missed, opacity)
                    : current.Palette.OutlineColor(note.Hand);
                if (color != slab.Applied) SetSlabColor(slab, color);
            }

            for (int i = _inUse.Count - 1; i >= 0; i--)
            {
                Slab slab = _inUse[i];
                if (slab.Frame == _frame) continue;
                Release(slab);
                _inUse[i] = _inUse[_inUse.Count - 1];
                _inUse.RemoveAt(_inUse.Count - 1);
            }
        }

        private bool _warnedNoFadedMaterial;
        private Material _fallbackMaterial;

        private Material SolidMaterial
        {
            get
            {
                if (noteMaterial != null) return noteMaterial;
                if (_fallbackMaterial == null) _fallbackMaterial = CreateFallbackMaterial();
                return _fallbackMaterial;
            }
        }

        private void SetLabelOpacity(Slab slab, float opacity)
        {
            slab.LabelOpacity = opacity;
            Color nameColor = labelColor;
            nameColor.a *= opacity;
            slab.Label.color = nameColor;
        }

        private void SetSlabColor(Slab slab, Color color)
        {
            slab.Renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);   // URP
            _block.SetColor(ColorId, color);       // Built-in, harmless either way
            slab.Renderer.SetPropertyBlock(_block);
            slab.Applied = color;
        }

        private TextMeshPro CreateLabel()
        {
            // Its own object, not a child of the slab: the slab's scale is
            // rewritten every frame and non-uniform, which would stretch the text.
            var go = new GameObject($"NoteLabel_{_slabsMade}");
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = labelFont;
            if (labelMaterial != null) tmp.fontSharedMaterial = labelMaterial;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = labelColor;
            // After its slab, which is in the same queue while it is fading.
            tmp.sortingOrder = 1;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = labelFontSize;
            tmp.fontSizeMin = labelFontSize * 0.5f;
            tmp.fontSize = labelFontSize;
            go.SetActive(false);
            return tmp;
        }

        private static void SetLabelVisible(TextMeshPro label, bool visible)
        {
            if (label != null && label.gameObject.activeSelf != visible)
                label.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------

        private static Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[NoteHighway] No usable shader found. Assign a material " +
                               "in the inspector -- runtime Shader.Find gets stripped from builds.");
                return null;
            }

            Debug.LogWarning($"[NoteHighway] Using runtime shader '{shader.name}'. This can be " +
                             "stripped from a device build -- assign a material in the inspector " +
                             "before you deploy.");
            return new Material(shader);
        }
    }
}
