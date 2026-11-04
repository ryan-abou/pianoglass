using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.Scoring;
using PianoGlass.Song;

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
    /// clamp maths. That means localScale is recomputed every frame, not
    /// once at spawn; Build() only creates the cubes.
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
    /// to hold it. One label per slab, built with the slabs; fine for an
    /// 8-bar song, to be pooled when MIDI file loading (F38) arrives.
    /// </summary>
    public class NoteHighway : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private SongClock clock;
        [Tooltip("Source of hit/miss judgements for the outcome colours. Optional: " +
                 "without it the slabs simply never change colour.")]
        [SerializeField] private ScoreKeeper scoreKeeper;

        [Header("Appearance")]
        [Tooltip("Assign a material made in the editor -- a runtime Shader.Find " +
                 "can be stripped from a device build.")]
        [SerializeField] private Material noteMaterial;
        [SerializeField] private Color noteColor = new Color(0.40f, 0.68f, 0.95f, 1f);
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

        private SongData _song;
        // index is the note's position in _song.Notes -- what
        // JudgementResult.noteIndex refers to. applied is the colour last
        // written, so a slab's property block is only touched when it changes.
        private readonly List<(SongNote note, int index, Renderer renderer)> _slabs =
            new List<(SongNote, int, Renderer)>();
        // Parallel to _slabs; null where labels are off or no font is assigned.
        private readonly List<TextMeshPro> _labels = new List<TextMeshPro>();
        private Color[] _applied = new Color[0];
        private NoteOutcome[] _outcome = new NoteOutcome[0];
        private float[] _judgedAt = new float[0];
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
            _block = new MaterialPropertyBlock();
            _song = SongData.TwinkleTwinkleLittleStar();
            _outcome = new NoteOutcome[_song.Notes.Count];
            _judgedAt = new float[_song.Notes.Count];
        }

        private void OnEnable()
        {
            if (scoreKeeper != null)
            {
                scoreKeeper.NoteJudged += OnNoteJudged;
                scoreKeeper.AttemptReset += ClearOutcomes;
            }
            if (calibration == null) return;
            calibration.Calibrated += OnCalibrated;
            calibration.PlacementValidChanged += OnPlacementValidChanged;
            if (calibration.IsCalibrated) Build();
        }

        private void OnDisable()
        {
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
            foreach (var (_, _, renderer) in _slabs)
                if (renderer != null) renderer.enabled = false;
            foreach (TextMeshPro label in _labels) SetLabelVisible(label, false);
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
            // Update() re-reads calibration.Layout every frame, so an
            // existing set of slabs already tracks a nudge without
            // rebuilding anything. Only build from scratch the first time
            // there's something to place -- otherwise a held nudge stick
            // (which fires this every frame) would churn GameObjects at
            // 72 fps for nothing.
            if (_slabs.Count == 0) Build();
        }

        // ------------------------------------------------------------------

        /// <summary>Creates one Cube per in-range note. Nothing about size or
        /// position is set here -- both depend on the song clock and are
        /// recomputed every frame in Update(), before the slab is ever shown
        /// (it starts disabled).</summary>
        private void Build()
        {
            foreach (var (_, _, renderer) in _slabs)
                if (renderer != null) Destroy(renderer.gameObject);
            _slabs.Clear();
            foreach (TextMeshPro label in _labels)
                if (label != null) Destroy(label.gameObject);
            _labels.Clear();

            if (calibration == null || !calibration.IsCalibrated) return;
            PianoKeyLayout layout = calibration.Layout;

            Material mat = noteMaterial != null ? noteMaterial : CreateFallbackMaterial();
            if (mat == null) return;

            bool labels = showNoteNames && labelFont != null;
            if (showNoteNames && labelFont == null)
                Debug.LogError("[NoteHighway] Note names on but no label font assigned -- " +
                               "run PianoGlass/Scene/Configure Note Names.");
            // Asked of the font itself, fallbacks included, so a font swap
            // that loses the glyph degrades to "F#" rather than a box.
            bool sharpGlyph = labels && labelFont.HasCharacter(NoteLabel.SharpGlyph, true, true);
            if (labels)
                Debug.Log("[NoteHighway] note names on, sharp drawn as " +
                          (sharpGlyph ? "U+266F" : "'#' (font has no U+266F)"));

            for (int index = 0; index < _song.Notes.Count; index++)
            {
                SongNote note = _song.Notes[index];
                if (!layout.Contains(note.Pitch)) continue; // outside this keyboard

                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Note_{note.Pitch}_{note.StartTime:0.00}";
                go.transform.SetParent(transform, false);

                // Colliders would fight with hand/controller physics for no gain.
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, noteColor);   // URP
                _block.SetColor(ColorId, noteColor);       // Built-in, harmless either way
                renderer.SetPropertyBlock(_block);

                _slabs.Add((note, index, renderer));
                _labels.Add(labels ? CreateLabel(note.Pitch, sharpGlyph, layout) : null);
            }
            _applied = new Color[_slabs.Count];
            for (int i = 0; i < _applied.Length; i++) _applied[i] = noteColor;

            Debug.Log($"[NoteHighway] built {_slabs.Count} note slabs for '{_song.Title}'");
        }

        private void Update()
        {
            if (clock == null || calibration == null || !calibration.IsPlacementValid || _slabs.Count == 0) return;

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

            for (int i = 0; i < _slabs.Count; i++)
            {
                var (note, index, renderer) = _slabs[i];
                if (renderer == null) continue;
                if (!layout.TryGetKey(note.Pitch, out PianoKey key)) continue;

                (float b, float height) = SlabGeometry.VerticalExtent(
                    note.StartTime, note.EndTime, songTime, speed, highwayHeight, noteGapMetres);

                // SlabGeometry's clamps alone are what cull a note that hasn't
                // arrived yet, or has fully passed, and what make a held
                // note's slab shrink into the key line as it plays rather
                // than sliding underneath it. No other visibility check is
                // needed on top of it.
                bool visible = height > 0f;
                renderer.enabled = visible;
                TextMeshPro label = _labels[i];
                if (!visible)
                {
                    SetLabelVisible(label, false);
                    continue;
                }

                // PianoKey.Center is the middle of the key's playing surface
                // (front to back), and Forward points at the player, so the
                // far edge -- away from the player -- is Center minus half
                // the key's own depth along Forward.
                Vector3 farEdge = key.Center - layout.Forward * (key.Depth * 0.5f + depthOffset);
                float centreHeight = b + height * 0.5f;
                Vector3 pos = farEdge + tiltedUp * centreHeight;

                renderer.transform.SetPositionAndRotation(pos, tiltRotation);
                renderer.transform.localScale = new Vector3(key.Width * widthFraction, height, slabThickness);

                if (label != null)
                {
                    bool fits = NoteLabel.Fits(height, labelHeight, labelPadding);
                    SetLabelVisible(label, fits);
                    if (fits)
                    {
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

                Color color = OutcomeTint.Slab(noteColor, _outcome[index], now - _judgedAt[index],
                                               OutcomeTint.Hit, OutcomeTint.Missed);
                if (color != _applied[i]) SetSlabColor(renderer, i, color);
            }
        }

        private void SetSlabColor(Renderer renderer, int slab, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);   // URP
            _block.SetColor(ColorId, color);       // Built-in, harmless either way
            renderer.SetPropertyBlock(_block);
            _applied[slab] = color;
        }

        private TextMeshPro CreateLabel(int pitch, bool sharpGlyph, PianoKeyLayout layout)
        {
            // Its own object, not a child of the slab: the slab's scale is
            // rewritten every frame and non-uniform, which would stretch the text.
            var go = new GameObject($"Label_{pitch}");
            go.transform.SetParent(transform, false);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.font = labelFont;
            if (labelMaterial != null) tmp.fontSharedMaterial = labelMaterial;
            tmp.text = NoteLabel.Name(pitch, sharpGlyph);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.color = labelColor;
            float width = layout.TryGetKey(pitch, out PianoKey key) ? key.Width * widthFraction : labelHeight * 2f;
            tmp.rectTransform.sizeDelta = new Vector2(width, labelHeight);
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
