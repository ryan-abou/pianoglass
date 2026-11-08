using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PianoGlass.Piano;
using PianoGlass.Song;

namespace PianoGlass.Highway
{
    /// <summary>
    /// The grid on the note highway, drawn to match the reference video
    /// (docs/feature-evaluation.md, "Highway grid: visual target") and
    /// nothing else -- it is the one visual element that does not follow
    /// PianoGlass's own palette.
    ///
    /// - Lane lines: thin, light, crisp, one per octave on every C's left
    ///   edge, the full height of the wall and the full width of the keys.
    /// - Beat lines: faint soft bands across the whole width, a little past
    ///   both ends of the keyboard, moving down with the notes.
    /// - Bar lines: the same band, brighter -- not thicker.
    /// - Bar numbers: a white bold digit in a dark rounded tag on the bar
    ///   line, just past the keyboard's left end.
    /// - Hit line: a solid dark bar along the keys' far edge.
    ///
    /// Everything is one mesh rebuilt each frame (a few dozen quads) in world
    /// space, on the plane NoteHighway.PlaneAxes defines, so the grid and the
    /// slabs can never disagree. Each quad is a rounded rectangle drawn by
    /// the PianoGlass/HighwayGrid shader's distance function, which also
    /// keeps every line at least about a pixel wide so nothing shimmers.
    /// Beat lines use DisplaySongTime, exactly like the slabs, so a note
    /// starting on a beat sits on its line all the way down.
    /// </summary>
    public class HighwayGrid : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private NoteHighway highway;
        [Tooltip("PianoGlass/HighwayGrid material, assigned by SceneSetup.")]
        [SerializeField] private Material gridMaterial;

        [Header("Lane lines")]
        // Alphas are low on purpose: the project renders in linear colour
        // space, where a blended alpha reads much brighter than its number
        // suggests (0.16 made the beat lines as loud as the lanes, 2026-11-02).
        [SerializeField] private Color laneColor = new Color(0.88f, 0.90f, 0.93f, 0.70f);
        [Tooltip("0.8 mm: 1.5 mm read too heavy on the headset (2026-11-02). Far " +
                 "away the shader's minimum on-screen width takes over, so a " +
                 "thinner value fades the far end rather than thinning it.")]
        [SerializeField] private float laneWidth = 0.0008f;

        [Header("Beat and bar lines")]
        [SerializeField] private Color beatColor = new Color(0.88f, 0.90f, 0.93f, 0.06f);
        [SerializeField] private Color barColor = new Color(0.88f, 0.90f, 0.93f, 0.15f);
        [SerializeField] private float beatWidth = 0.003f;
        [Tooltip("How far beat and bar lines run past each end of the keyboard.")]
        [SerializeField] private float lineOverhang = 0.10f;
        [Tooltip("Length over which a beat line fades out at each end.")]
        [SerializeField] private float lineEndFade = 0.06f;
        [Range(0f, 1f)]
        [Tooltip("Top fraction of the wall over which beat lines and tags fade " +
                 "in, so they arrive rather than pop.")]
        [SerializeField] private float topFadeFraction = 0.2f;

        [Header("Bar number tags")]
        [SerializeField] private Color tagColor = new Color(0.03f, 0.03f, 0.04f, 0.92f);
        [SerializeField] private Color tagTextColor = Color.white;
        [Tooltip("Tags stand upright facing the player, not flat on the " +
                 "runway -- lying flat, the grazing view squashed them to " +
                 "slivers. The reference video's tags face the viewer too.")]
        [SerializeField] private Vector2 tagSize = new Vector2(0.046f, 0.028f);
        [SerializeField] private float tagCornerRadius = 0.006f;
        [Tooltip("Gap between the tag's right side and the keyboard's left end.")]
        [SerializeField] private float tagGap = 0.006f;
        [SerializeField] private float tagFontSize = 0.22f;

        [Header("Hit line")]
        [SerializeField] private Color hitLineColor = new Color(0.02f, 0.02f, 0.03f, 0.95f);
        [Tooltip("Thickness of the hit line, measured up the wall from the key " +
                 "line. It starts at the key line rather than centring on it: " +
                 "centred, its lower half hung over the key tops (2026-11-02).")]
        [SerializeField] private float hitLineWidth = 0.012f;

        [Header("Depth")]
        [Tooltip("Metres the grid sits behind the slabs' centre plane (away from " +
                 "the player), clear of their back face so lines never flicker " +
                 "through a slab.")]
        [SerializeField] private float standoff = 0.004f;

        // A quad extends this far past its rectangle so the anti-aliased
        // edge (and a far-away line's minimum width) has room to fade.
        private const float Pad = 0.004f;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _local = new List<Vector2>();
        private readonly List<Vector3> _rect = new List<Vector3>();
        private readonly List<int> _tris = new List<int>();
        private readonly List<BeatLine> _beats = new List<BeatLine>();
        private readonly List<int> _lanes = new List<int>();
        private readonly List<TextMeshPro> _tagText = new List<TextMeshPro>();
        private readonly float[] _xs = new float[4];
        private SongData _loggedSong;

        private void Awake()
        {
            if (highway == null) highway = FindAnyObjectByType<NoteHighway>();

            // The mesh is in world space, so its object must stay at the
            // origin with no scale -- hence a root object of its own rather
            // than this transform, which SceneSetup may put anywhere.
            var go = new GameObject("HighwayGridMesh");
            go.AddComponent<MeshFilter>().sharedMesh = _mesh = new Mesh { name = "HighwayGrid" };
            _mesh.MarkDynamic();
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = gridMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;

            if (gridMaterial == null)
                Debug.LogError("[HighwayGrid] No grid material assigned -- run " +
                               "PianoGlass/Scene/Configure Highway Grid.");
        }

        private void LateUpdate()
        {
            KeyboardCalibration calibration = highway != null ? highway.Calibration : null;
            SongClock clock = highway != null ? highway.Clock : null;
            if (gridMaterial == null || clock == null || calibration == null ||
                !calibration.IsCalibrated || !calibration.IsPlacementValid || highway.Song == null)
            {
                Hide();
                return;
            }

            Rebuild(calibration.Layout, clock, highway.Song);
            _renderer.enabled = true;
        }

        private void OnDestroy()
        {
            if (_renderer != null) Destroy(_renderer.gameObject);
            if (_mesh != null) Destroy(_mesh);
        }

        private void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
            foreach (TextMeshPro t in _tagText) t.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------

        private void Rebuild(PianoKeyLayout layout, SongClock clock, SongData song)
        {
            _verts.Clear(); _colors.Clear(); _local.Clear(); _rect.Clear(); _tris.Clear();

            (Vector3 up, Vector3 normal) = highway.PlaneAxes(layout);
            Vector3 right = layout.Right;
            float spacing = layout.WhiteKeySpacing;
            float height = highway.HighwayHeight;
            float speed = highway.Speed;

            // x = 0 is the lowest key's centre line, on the white keys' far
            // edge, the same point every slab's bottom starts from.
            Vector3 origin = layout.LowFrontEdge
                             - layout.Forward * (layout.WhiteKeyDepth + highway.DepthOffset)
                             - normal * standoff;
            float leftEnd = -spacing * 0.5f;
            float rightEnd = (PianoKeyLayout.CountWhiteKeys(layout.LowestNote, layout.HighestNote) - 1) * spacing
                             + spacing * 0.5f;

            Vector3 At(float x, float y) => origin + right * x + up * y;

            // Lane lines, one per octave.
            BeatGrid.LaneNotes(layout.LowestNote, layout.HighestNote, _lanes);
            foreach (int c in _lanes)
            {
                float x = PianoKeyLayout.CountWhiteKeysBelow(layout.LowestNote, c) * spacing - spacing * 0.5f;
                AddRect(At(x, height * 0.5f), up, right, height * 0.5f, laneWidth * 0.5f, 0f, laneColor, 0f);
            }

            // Hit line, on the runway side of the key line only, so it never
            // covers the keys; a slab's bottom stops on its near edge.
            AddRect(At((leftEnd + rightEnd) * 0.5f, hitLineWidth * 0.5f), right, up,
                    (rightEnd - leftEnd) * 0.5f, hitLineWidth * 0.5f, 0f, hitLineColor, 0f);

            // Beat and bar lines, and the bar tags.
            double songTime = clock.DisplaySongTime;
            // In a practice loop the lines stop at its end, like its notes.
            double earliest = -clock.LeadInSeconds, latest = song.Duration;
            if (clock.Loop is LoopRange loop)
            {
                earliest = System.Math.Min(earliest, loop.WrapTo);
                latest = loop.NotesUntil;
            }
            BeatGrid.CollectVisible(song.TempoMap, earliest, latest,
                                    songTime, highway.LeadTimeSeconds, _beats);

            float lineLeft = leftEnd - lineOverhang;
            float lineRight = rightEnd + lineOverhang;
            float fadeStart = height * (1f - topFadeFraction);
            int tagCount = 0;

            foreach (BeatLine beat in _beats)
            {
                float y = (float)(beat.Time - songTime) * speed;
                float fade = topFadeFraction > 0f && y > fadeStart
                    ? Mathf.Clamp01((height - y) / (height - fadeStart))
                    : 1f;
                Color c = beat.IsBarLine ? barColor : beatColor;
                c.a *= fade;
                AddRect(At((lineLeft + lineRight) * 0.5f, y), right, up,
                        (lineRight - lineLeft) * 0.5f, beatWidth * 0.5f, 0f, c, lineEndFade);

                if (beat.IsBarLine && beat.Bar >= 1)
                {
                    Vector3 centre = At(leftEnd - tagGap - tagSize.x * 0.5f, y) + normal * (standoff * 0.5f);
                    (Vector3 tagRight, Vector3 tagUp, Vector3 tagNormal) = TagAxes(centre, right, layout.Up);
                    Color bg = tagColor;
                    bg.a *= fade;
                    AddRect(centre, tagRight, tagUp, tagSize.x * 0.5f, tagSize.y * 0.5f, tagCornerRadius, bg, 0f);
                    PlaceTag(tagCount++, beat.Bar, centre + tagNormal * 0.001f, tagUp, tagNormal, fade);
                }
            }

            for (int i = tagCount; i < _tagText.Count; i++)
                _tagText[i].gameObject.SetActive(false);

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _local);
            _mesh.SetUVs(1, _rect);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();

            if (_loggedSong != song)
            {
                _loggedSong = song;
                Debug.Log($"[HighwayGrid] drawing {_lanes.Count} lane lines over notes " +
                          $"{layout.LowestNote}-{layout.HighestNote}, {song.TempoMap.TimeSignatureText} at {song.Bpm} BPM");
            }
        }

        /// <summary>
        /// A tag's axes: facing the player's head, upright (its up as close
        /// to the keyboard's Up as facing allows), its right along the
        /// keyboard. Falls back to the keyboard's own frame with no camera.
        /// </summary>
        private static (Vector3 right, Vector3 up, Vector3 normal) TagAxes(Vector3 centre, Vector3 keyRight, Vector3 keyUp)
        {
            Camera cam = Camera.main;
            Vector3 normal = cam != null ? cam.transform.position - centre : Vector3.Cross(keyRight, keyUp);
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(keyRight, keyUp);
            normal.Normalize();
            Vector3 right = (keyRight - normal * Vector3.Dot(keyRight, normal)).normalized;
            Vector3 up = Vector3.Cross(normal, right);
            if (Vector3.Dot(up, keyUp) < 0f) up = -up;
            return (right, up, normal);
        }

        /// <summary>
        /// One rounded rectangle centred on centre: halfLength along
        /// `along`, halfWidth along `across`. endFade &gt; 0 splits it into
        /// three quads along its length whose alpha ramps 0 → 1 → 1 → 0, all
        /// sharing one distance field, so the fade has no seams.
        /// </summary>
        private void AddRect(Vector3 centre, Vector3 along, Vector3 across,
                             float halfLength, float halfWidth, float radius, Color color, float endFade)
        {
            if (color.a <= 0f) return;

            float outer = halfLength + Pad;
            float fade = Mathf.Clamp(endFade, 0f, halfLength);
            // Reused, not allocated: this runs a few dozen times a frame
            // and a GC hitch on Quest is a dropped frame.
            float[] xs = _xs;
            int count;
            if (fade > 0f)
            {
                xs[0] = -outer; xs[1] = -halfLength + fade; xs[2] = halfLength - fade; xs[3] = outer;
                count = 4;
            }
            else
            {
                xs[0] = -outer; xs[1] = outer;
                count = 2;
            }

            var rect = new Vector3(halfLength, halfWidth, radius);
            float w = halfWidth + Pad;
            int start = _verts.Count;

            for (int i = 0; i < count; i++)
            {
                float x = xs[i];
                bool end = fade > 0f && (i == 0 || i == count - 1);
                Color c = color;
                if (end) c.a = 0f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float y = side * w;
                    _verts.Add(centre + along * x + across * y);
                    _colors.Add(c);
                    _local.Add(new Vector2(x, y));
                    _rect.Add(rect);
                }
            }

            for (int i = 0; i < count - 1; i++)
            {
                int a = start + i * 2;
                _tris.Add(a); _tris.Add(a + 1); _tris.Add(a + 3);
                _tris.Add(a); _tris.Add(a + 3); _tris.Add(a + 2);
            }
        }

        private void PlaceTag(int index, int bar, Vector3 position, Vector3 up, Vector3 normal, float fade)
        {
            while (_tagText.Count <= index)
            {
                var go = new GameObject($"BarTag_{_tagText.Count}");
                go.transform.SetParent(transform, false);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.rectTransform.sizeDelta = tagSize;
                tmp.fontSize = tagFontSize;
                _tagText.Add(tmp);
            }

            TextMeshPro t = _tagText[index];
            t.gameObject.SetActive(true);
            // TMP text reads correctly when its +z points away from the
            // viewer, so face it along -normal with the wall's up.
            t.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-normal, up));
            string label = bar.ToString();
            if (t.text != label) t.text = label;
            Color c = tagTextColor;
            c.a *= fade;
            t.color = c;
        }

    }
}
