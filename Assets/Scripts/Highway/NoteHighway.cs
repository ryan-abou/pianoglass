using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Piano;
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
    /// hardest depth judgement a Quest 2 can ask of someone.
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
    /// </summary>
    public class NoteHighway : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private SongClock clock;

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
                 "top toward the player, which is the direction that makes the " +
                 "wall easier to read from a seated view; 0 is upright.")]
        [SerializeField] private float tiltDegrees = 0f;

        [Header("Beat lines")]
        [Tooltip("Horizontal lines across the wall, one per beat, falling with " +
                 "the notes. A note that starts on a beat sits on its line all " +
                 "the way down, so the beat is something to lock onto.")]
        [SerializeField] private bool showBeatLines = true;
        [SerializeField] private Color beatLineColor = new Color(0.30f, 0.33f, 0.36f, 1f);
        [SerializeField] private Color barLineColor = new Color(0.72f, 0.76f, 0.80f, 1f);
        [Tooltip("Lead-in beats: the count-in falling into the first note.")]
        [SerializeField] private Color countInLineColor = new Color(0.95f, 0.72f, 0.28f, 1f);
        [SerializeField] private float beatLineThickness = 0.0015f;
        [SerializeField] private float barLineThickness = 0.003f;
        [Tooltip("How far past the song's lowest and highest keys the lines run, " +
                 "in metres. They span the song's range, not all 88 keys -- a " +
                 "1.2 m line across the whole view would be clutter.")]
        [SerializeField] private float beatLinePadding = 0.03f;

        private SongData _song;
        private readonly List<(SongNote note, Renderer renderer)> _slabs = new List<(SongNote, Renderer)>();
        private readonly List<Renderer> _beatLines = new List<Renderer>();
        private readonly List<BeatLine> _visibleBeats = new List<BeatLine>();
        private int _lowestPitch, _highestPitch;
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
            _block = new MaterialPropertyBlock();
            _song = SongData.TwinkleTwinkleLittleStar();
        }

        private void OnEnable()
        {
            if (calibration == null) return;
            calibration.Calibrated += OnCalibrated;
            calibration.PlacementValidChanged += OnPlacementValidChanged;
            if (calibration.IsCalibrated) Build();
        }

        private void OnDisable()
        {
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
            foreach (var (_, renderer) in _slabs)
                if (renderer != null) renderer.enabled = false;
            HideBeatLines(0);
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
            foreach (var (_, renderer) in _slabs)
                if (renderer != null) Destroy(renderer.gameObject);
            _slabs.Clear();
            foreach (Renderer renderer in _beatLines)
                if (renderer != null) Destroy(renderer.gameObject);
            _beatLines.Clear();

            if (calibration == null || !calibration.IsCalibrated) return;
            PianoKeyLayout layout = calibration.Layout;

            Material mat = noteMaterial != null ? noteMaterial : CreateFallbackMaterial();
            if (mat == null) return;

            _lowestPitch = int.MaxValue;
            _highestPitch = int.MinValue;
            foreach (SongNote note in _song.Notes)
            {
                if (!layout.Contains(note.Pitch)) continue; // outside this keyboard

                Renderer renderer = CreateCube($"Note_{note.Pitch}_{note.StartTime:0.00}", mat);
                SetColor(renderer, noteColor);
                _slabs.Add((note, renderer));

                _lowestPitch = Mathf.Min(_lowestPitch, note.Pitch);
                _highestPitch = Mathf.Max(_highestPitch, note.Pitch);
            }

            // A fixed pool, enough for every beat the wall can hold at once
            // (both ends of the window inclusive), recoloured as beats pass.
            if (showBeatLines && _slabs.Count > 0)
            {
                double spb = BeatGrid.SecondsPerBeat(_song.Bpm);
                int pool = spb > 0.0 ? (int)System.Math.Ceiling(leadTimeSeconds / spb) + 2 : 0;
                for (int i = 0; i < pool; i++)
                    _beatLines.Add(CreateCube($"BeatLine_{i}", mat));
            }

            Debug.Log($"[NoteHighway] built {_slabs.Count} note slabs and {_beatLines.Count} beat lines for '{_song.Title}'");
        }

        private Renderer CreateCube(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);

            // Colliders would fight with hand/controller physics for no gain.
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        private void SetColor(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);   // URP
            _block.SetColor(ColorId, color);       // Built-in, harmless either way
            renderer.SetPropertyBlock(_block);
        }

        private void Update()
        {
            if (clock == null || calibration == null || !calibration.IsPlacementValid || _slabs.Count == 0) return;

            PianoKeyLayout layout = calibration.Layout;
            // Where the song will be when this frame reaches the display, not
            // where it is now -- otherwise every note lands on the key line
            // one display latency late, and the player presses late with it.
            double songTime = clock.DisplaySongTime;
            float speed = highwayHeight / Mathf.Max(0.01f, leadTimeSeconds);

            // Shared by every slab this frame: the wall's orientation and its
            // vertical axis once tilt is applied. Rotating the whole
            // orientation about layout.Right (rather than just nudging Up)
            // keeps Up and Forward orthogonal and rotating together, so the
            // wall stays a flat plane instead of shearing.
            Quaternion tiltRotation = Quaternion.AngleAxis(tiltDegrees, layout.Right)
                                       * Quaternion.LookRotation(layout.Forward, layout.Up);
            Vector3 tiltedUp = tiltRotation * Vector3.up;

            foreach (var (note, renderer) in _slabs)
            {
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
                if (!visible) continue;

                // PianoKey.Center is the middle of the key's playing surface
                // (front to back), and Forward points at the player, so the
                // far edge -- away from the player -- is Center minus half
                // the key's own depth along Forward.
                Vector3 farEdge = key.Center - layout.Forward * (key.Depth * 0.5f + depthOffset);
                float centreHeight = b + height * 0.5f;
                Vector3 pos = farEdge + tiltedUp * centreHeight;

                renderer.transform.SetPositionAndRotation(pos, tiltRotation);
                renderer.transform.localScale = new Vector3(key.Width * widthFraction, height, slabThickness);
            }

            UpdateBeatLines(layout, songTime, speed, tiltRotation, tiltedUp);
        }

        /// <summary>One line per visible beat, at the same height a note
        /// starting on that beat has its bottom -- both are (time - songTime)
        /// x speed -- so lines and notes fall together. Drawn from
        /// DisplaySongTime like the notes, so they hit the key line on the
        /// beat as the player sees it.</summary>
        private void UpdateBeatLines(PianoKeyLayout layout, double songTime, float speed,
                                     Quaternion tiltRotation, Vector3 tiltedUp)
        {
            if (_beatLines.Count == 0) return;

            // Ends of the line: the far edges of the white keys at (or just
            // outside) the song's lowest and highest notes. White keys
            // because they reach furthest back -- the same edge the note
            // slabs stand on.
            int low = PianoKeyLayout.IsBlackKey(_lowestPitch) ? _lowestPitch - 1 : _lowestPitch;
            int high = PianoKeyLayout.IsBlackKey(_highestPitch) ? _highestPitch + 1 : _highestPitch;
            if (!layout.TryGetKey(low, out PianoKey lowKey) || !layout.TryGetKey(high, out PianoKey highKey))
            {
                HideBeatLines(0);
                return;
            }

            // Just behind the note slabs, so a note drawn on its line covers
            // it instead of z-fighting with it.
            float behind = depthOffset + slabThickness;
            Vector3 lowEdge = lowKey.Center - layout.Forward * (lowKey.Depth * 0.5f + behind)
                              - layout.Right * (lowKey.Width * 0.5f + beatLinePadding);
            Vector3 highEdge = highKey.Center - layout.Forward * (highKey.Depth * 0.5f + behind)
                               + layout.Right * (highKey.Width * 0.5f + beatLinePadding);
            Vector3 mid = (lowEdge + highEdge) * 0.5f;
            float length = Vector3.Distance(lowEdge, highEdge);

            int count = BeatGrid.CollectVisible(_song.Bpm, _song.BeatsPerBar, -clock.LeadInSeconds,
                                                _song.Duration, songTime, leadTimeSeconds, _visibleBeats);
            count = Mathf.Min(count, _beatLines.Count);

            for (int i = 0; i < count; i++)
            {
                BeatLine beat = _visibleBeats[i];
                Renderer renderer = _beatLines[i];
                if (renderer == null) continue;

                float thickness = beat.IsBarLine ? barLineThickness : beatLineThickness;
                Color color = beat.IsCountIn ? countInLineColor
                            : beat.IsBarLine ? barLineColor
                            : beatLineColor;

                float h = (float)(beat.Time - songTime) * speed;
                // Centred on its height, but never poking below the key line.
                Vector3 pos = mid + tiltedUp * Mathf.Max(h, thickness * 0.5f);

                renderer.transform.SetPositionAndRotation(pos, tiltRotation);
                renderer.transform.localScale = new Vector3(length, thickness, slabThickness);
                SetColor(renderer, color);
                renderer.enabled = true;
            }
            HideBeatLines(count);
        }

        private void HideBeatLines(int from)
        {
            for (int i = from; i < _beatLines.Count; i++)
                if (_beatLines[i] != null) _beatLines[i].enabled = false;
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
