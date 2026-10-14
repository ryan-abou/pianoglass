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

        [Header("Highway")]
        [Tooltip("Seconds of warning before a note is due -- how far up the wall it spawns.")]
        [SerializeField] private float leadTimeSeconds = 3f;
        [Tooltip("Height of the wall, in metres, from the key line to where a note " +
                 "spawns.")]
        [SerializeField] private float highwayHeight = 0.5f;
        [Tooltip("Small standoff behind the key's far edge (away from the player), " +
                 "so the wall doesn't clip into the key body.")]
        [SerializeField] private float depthOffset = 0.02f;
        [Tooltip("Degrees the wall leans, about layout.Right. Positive tips the " +
                 "top toward the player, which is the direction that makes the " +
                 "wall easier to read from a seated view; 0 is upright.")]
        [SerializeField] private float tiltDegrees = 0f;

        /// <summary>Same standoff used to place the status Canvas at the wall's
        /// depth -- CanvasCalibrationFollower reads this rather than carrying
        /// its own copy of the number, so the two can't drift apart.</summary>
        public float DepthOffset => depthOffset;

        private SongData _song;
        private readonly List<(SongNote note, Renderer renderer)> _slabs = new List<(SongNote, Renderer)>();
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

            if (calibration == null || !calibration.IsCalibrated) return;
            PianoKeyLayout layout = calibration.Layout;

            Material mat = noteMaterial != null ? noteMaterial : CreateFallbackMaterial();
            if (mat == null) return;

            foreach (SongNote note in _song.Notes)
            {
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

                _slabs.Add((note, renderer));
            }

            Debug.Log($"[NoteHighway] built {_slabs.Count} note slabs for '{_song.Title}'");
        }

        private void Update()
        {
            if (clock == null || calibration == null || !calibration.IsPlacementValid || _slabs.Count == 0) return;

            PianoKeyLayout layout = calibration.Layout;
            double songTime = clock.SongTime;
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
                    note.StartTime, note.EndTime, songTime, speed, highwayHeight);

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
