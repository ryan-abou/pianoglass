using System;
using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Song;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Day four's payoff: press a real key, the correct virtual key lights up.
    ///
    /// When this works, the two halves of PianoGlass are joined — the MIDI
    /// stream and the physical geometry agree with each other — and everything
    /// after it (falling notes, wait-mode, scoring) is ordinary Unity work.
    ///
    /// It is also your permanent alignment check. Turn on "Show idle keys",
    /// look at your keyboard, and any calibration drift is immediately obvious.
    ///
    /// A Quest 2 has no depth sensor, so a virtual slab painted over a key is
    /// always drawn on top of the player's hand -- passthrough or not. Every
    /// mode here except FullOverlay exists to paint less of the keyboard, so
    /// there is less overlay for a hand to disappear under. See DisplayMode.
    /// </summary>
    public class PianoKeyHighlighter : MonoBehaviour
    {
        /// <summary>
        /// FullOverlay    every key painted at rest, full key size -- the
        ///                original behaviour, kept as the baseline to A/B
        ///                the others against.
        /// Translucent    FullOverlay, but every colour's alpha is
        ///                translucentAlpha instead of opaque. Needs
        ///                translucentMaterial (a Transparent-surface asset)
        ///                or the alpha has nothing to show through.
        /// ActiveKeysOnly nothing painted at rest. A key appears only once a
        ///                note due within activeKeyLookaheadSeconds needs it,
        ///                or while the real key is actually held (green) --
        ///                both read from SongClock/MidiInput every frame.
        /// EdgeMarkers    the same "only when relevant" rule as
        ///                ActiveKeysOnly, but each key shows as a thin bar
        ///                across its far portion (edgeMarkerDepthFraction of
        ///                its depth) instead of the full key, clear of where
        ///                a resting finger actually touches.
        /// </summary>
        public enum DisplayMode { FullOverlay, Translucent, ActiveKeysOnly, EdgeMarkers }

        [Header("Wiring")]
        [SerializeField] private KeyboardCalibration calibration;
        [Tooltip("Needed for ActiveKeysOnly/EdgeMarkers, which show a key ahead " +
                 "of when its note is due. Unused by FullOverlay/Translucent.")]
        [SerializeField] private SongClock clock;

        [Header("Appearance")]
        [Tooltip("Assign a material made in the editor. Leave empty and one is " +
                 "created at runtime, but a runtime Shader.Find can be stripped " +
                 "from the build — assigning it here is the safe path.")]
        [SerializeField] private Material keyMaterial;

        [Tooltip("A Transparent-surface material for Translucent mode, created " +
                 "and assigned by SceneSetup. Without it, Translucent still " +
                 "writes a lower alpha into the colour, but keyMaterial's " +
                 "Opaque surface ignores alpha entirely, so nothing looks any " +
                 "different -- this is the trap a runtime Shader.Find would " +
                 "have hit too, just one step later.")]
        [SerializeField] private Material translucentMaterial;

        [Tooltip("Draw every key faintly so you can see whether the calibration " +
                 "still lines up with the real keyboard. Only affects " +
                 "FullOverlay/Translucent -- ActiveKeysOnly/EdgeMarkers never " +
                 "show an idle key regardless, that being their whole point.")]
        [SerializeField] private bool showIdleKeys = true;

        [SerializeField] private Color idleWhiteKey = new Color(0.55f, 0.60f, 0.50f, 1f);
        [SerializeField] private Color idleBlackKey = new Color(0.16f, 0.18f, 0.14f, 1f);
        [SerializeField] private Color pressedKey = new Color(0.66f, 0.78f, 0.49f, 1f);

        [Tooltip("Height of the key slab, in metres. Thin enough to sit on the real key.")]
        [SerializeField] private float slabThickness = 0.004f;

        [Tooltip("Lift everything slightly so it doesn't z-fight with anything.")]
        [SerializeField] private float surfaceOffset = 0.001f;

        [Header("Display Mode")]
        [Tooltip("ActiveKeysOnly is the default: the old FullOverlay behaviour " +
                 "painted all 88 keys at rest, which hides the player's hand " +
                 "even with passthrough on. Cycle through the others with " +
                 "OvrKeyOverlayInput (left X) to compare them on the headset.")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.ActiveKeysOnly;

        [Tooltip("Alpha applied to every colour in Translucent mode. Default " +
                 "0.25 -- faint enough to still show a hand through it.")]
        [SerializeField] private float translucentAlpha = 0.25f;

        [Tooltip("How far ahead of its due time a note's key appears in " +
                 "ActiveKeysOnly/EdgeMarkers. Default 0.5 s.")]
        [SerializeField] private float activeKeyLookaheadSeconds = 0.5f;

        [Tooltip("EdgeMarkers' bar depth, as a fraction of the key's own " +
                 "front-to-back length. Kept small and placed at the far " +
                 "edge so it stays clear of where a finger rests.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float edgeMarkerDepthFraction = 0.2f;

        public DisplayMode Mode => displayMode;

        private readonly Dictionary<int, Renderer> _renderers = new Dictionary<int, Renderer>();
        private readonly HashSet<int> _dueSoon = new HashSet<int>();
        private MaterialPropertyBlock _block;
        private Material _opaqueMaterial;
        private PianoKeyLayout _layout;
        private SongData _song;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            // FindAnyObjectByType, not FindFirstObjectByType: the "First" variant
            // is deprecated because it depends on instance-ID ordering, and we
            // only ever expect one calibration in the scene anyway.
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (clock == null) clock = FindAnyObjectByType<SongClock>();

            // Same hard-coded song NoteHighway uses. Each reads it
            // independently rather than sharing a reference, the same way
            // each independently finds its own KeyboardCalibration -- there
            // is exactly one song right now, so there is nothing to drift.
            _song = SongData.TwinkleTwinkleLittleStar();
        }

        private void OnEnable()
        {
            if (calibration != null)
            {
                calibration.Calibrated += Rebuild;
                if (calibration.IsCalibrated) Rebuild(calibration.Layout);
            }

            if (MidiInput.Instance != null)
            {
                MidiInput.Instance.NoteOn += OnNoteOn;
                MidiInput.Instance.NoteOff += OnNoteOff;
            }
        }

        private void OnDisable()
        {
            if (calibration != null) calibration.Calibrated -= Rebuild;
            if (MidiInput.Instance != null)
            {
                MidiInput.Instance.NoteOn -= OnNoteOn;
                MidiInput.Instance.NoteOff -= OnNoteOff;
            }
        }

        /// <summary>
        /// ActiveKeysOnly/EdgeMarkers need to keep checking "is a note due
        /// soon" against the live SongClock -- unlike FullOverlay/Translucent,
        /// which only ever change on a MIDI event or a Rebuild. Held keys are
        /// left alone here: OnNoteOn already made them visible and green, and
        /// this loop has no business overriding that.
        /// </summary>
        private void Update()
        {
            if (clock == null || _renderers.Count == 0) return;
            if (displayMode != DisplayMode.ActiveKeysOnly && displayMode != DisplayMode.EdgeMarkers) return;

            ActiveNoteSelector.CollectDueSoonPitches(_song.Notes, clock.SongTime, activeKeyLookaheadSeconds, _dueSoon);

            foreach (var kv in _renderers)
            {
                int note = kv.Key;
                Renderer renderer = kv.Value;
                if (renderer == null) continue;
                if (MidiInput.Instance != null && MidiInput.Instance.IsHeld(note)) continue;

                bool relevant = _dueSoon.Contains(note);
                renderer.enabled = relevant;
                if (relevant) SetColor(note, PianoKeyLayout.IsBlackKey(note) ? idleBlackKey : idleWhiteKey);
            }
        }

        // ------------------------------------------------------------------

        private void Rebuild(PianoKeyLayout layout)
        {
            _layout = layout;

            // Nudging fires this every frame while the stick is held. Destroying
            // and recreating 88 GameObjects at 72 fps would tank the frame rate
            // and churn the GC, so when the key set is unchanged just move what
            // is already there.
            if (layout != null && _renderers.Count == layout.KeyCount)
            {
                Reposition(layout);
                return;
            }

            foreach (var r in _renderers.Values)
                if (r != null) Destroy(r.gameObject);
            _renderers.Clear();

            if (layout == null) return;

            _opaqueMaterial = keyMaterial != null ? keyMaterial : CreateFallbackMaterial();

            foreach (var kv in layout.Keys)
            {
                PianoKey key = kv.Value;

                // A thin cube rather than a quad: cubes have no "which way does
                // the normal face" problem, so it either appears or your
                // calibration is wrong — never "it's there but facing away".
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Key_{MidiInput.NoteName(key.Note)}";
                go.transform.SetParent(transform, false);

                // Colliders would fight with hand/controller physics for no gain.
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = go.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _renderers[key.Note] = renderer;
                ApplyKeyTransform(renderer, key, layout);
                ApplyRestState(key.Note, key.IsBlack);
            }

            ApplyMaterialForMode();

            Debug.Log($"[Highlighter] built {_renderers.Count} keys");
        }

        /// <summary>Move existing key objects to a shifted layout. No allocation,
        /// no Destroy — safe to call every frame while nudging.</summary>
        private void Reposition(PianoKeyLayout layout)
        {
            foreach (var kv in layout.Keys)
            {
                if (!_renderers.TryGetValue(kv.Key, out var renderer) || renderer == null) continue;
                ApplyKeyTransform(renderer, kv.Value, layout);
            }
        }

        /// <summary>Position and scale for one key's slab under the current
        /// display mode. Every mode except EdgeMarkers uses the full key;
        /// EdgeMarkers swaps in EdgeMarkerGeometry's far-edge bar instead.
        /// Reused by Rebuild, Reposition and a mode change, so geometry never
        /// drifts between the three call sites.</summary>
        private void ApplyKeyTransform(Renderer renderer, PianoKey key, PianoKeyLayout layout)
        {
            Vector3 center = key.Center + layout.Up * surfaceOffset;
            float depth = key.Depth;

            if (displayMode == DisplayMode.EdgeMarkers)
            {
                (float barDepth, float centerOffset) = EdgeMarkerGeometry.FarEdgeBar(key.Depth, edgeMarkerDepthFraction);
                center += layout.Forward * centerOffset;
                depth = barDepth;
            }

            renderer.transform.SetPositionAndRotation(center, key.Rotation);
            renderer.transform.localScale = new Vector3(key.Width, slabThickness, depth);
        }

        /// <summary>Which material every key renderer should currently be
        /// using -- translucentMaterial in Translucent mode (its Transparent
        /// surface is what makes the alpha SetColor writes actually visible),
        /// keyMaterial/the fallback everywhere else.</summary>
        private void ApplyMaterialForMode()
        {
            Material mat = (displayMode == DisplayMode.Translucent && translucentMaterial != null)
                ? translucentMaterial
                : _opaqueMaterial;
            if (mat == null) return;

            foreach (var renderer in _renderers.Values)
                if (renderer != null) renderer.sharedMaterial = mat;
        }

        private void OnNoteOn(int note, int velocity, double time)
        {
            // Velocity brightens the flash a little — free feedback on touch,
            // and a hint of the dynamics scoring you can add later.
            float t = Mathf.InverseLerp(20f, 110f, velocity);
            SetColor(note, Color.Lerp(pressedKey * 0.7f, pressedKey, t), forceVisible: true);
        }

        private void OnNoteOff(int note, double time)
        {
            if (!_renderers.ContainsKey(note)) return;
            ApplyRestState(note, PianoKeyLayout.IsBlackKey(note));
        }

        // ------------------------------------------------------------------

        /// <summary>Public so the note highway and wait-mode can tint keys too.</summary>
        public void SetColor(int note, Color color, bool forceVisible = false)
        {
            if (!_renderers.TryGetValue(note, out var renderer) || renderer == null) return;

            if (forceVisible) renderer.enabled = true;

            if (displayMode == DisplayMode.Translucent)
                color = new Color(color.r, color.g, color.b, translucentAlpha);

            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);   // URP
            _block.SetColor(ColorId, color);       // Built-in, harmless either way
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>A key's resting appearance under the current display
        /// mode: visible with its idle colour for FullOverlay/Translucent
        /// (gated by showIdleKeys, as before), hidden for ActiveKeysOnly/
        /// EdgeMarkers (Update() is what shows it again, once relevant).
        /// Skipped for a held key so it doesn't stomp the pressed-key flash.</summary>
        private void ApplyRestState(int note, bool isBlack)
        {
            if (!_renderers.TryGetValue(note, out var renderer) || renderer == null) return;
            if (MidiInput.Instance != null && MidiInput.Instance.IsHeld(note)) return;

            switch (displayMode)
            {
                case DisplayMode.ActiveKeysOnly:
                case DisplayMode.EdgeMarkers:
                    renderer.enabled = false;
                    break;

                default: // FullOverlay, Translucent
                    renderer.enabled = showIdleKeys;
                    SetColor(note, isBlack ? idleBlackKey : idleWhiteKey);
                    break;
            }
        }

        /// <summary>Public so external callers can put a key back to its
        /// resting appearance without reaching into display-mode internals.</summary>
        public void SetIdle(int note, bool isBlack) => ApplyRestState(note, isBlack);

        [ContextMenu("Toggle idle keys")]
        public void ToggleIdleKeys()
        {
            showIdleKeys = !showIdleKeys;
            foreach (var kv in _renderers)
                ApplyRestState(kv.Key, PianoKeyLayout.IsBlackKey(kv.Key));
        }

        /// <summary>Advances to the next DisplayMode, wrapping around, and
        /// re-applies geometry/material/rest-state to every existing key --
        /// no Destroy, no rebuild, just the same objects reused under the
        /// new mode. Driven by OvrKeyOverlayInput (left X); also reachable
        /// from the context menu for testing without a headset.</summary>
        [ContextMenu("Cycle display mode")]
        public void CycleDisplayMode()
        {
            var values = (DisplayMode[])Enum.GetValues(typeof(DisplayMode));
            int next = (Array.IndexOf(values, displayMode) + 1) % values.Length;
            displayMode = values[next];

            ApplyMaterialForMode();
            if (_layout == null) return;

            foreach (var kv in _renderers)
            {
                if (!_layout.TryGetKey(kv.Key, out PianoKey key)) continue;
                ApplyKeyTransform(kv.Value, key, _layout);
                ApplyRestState(kv.Key, key.IsBlack);
            }
        }

        private static Material CreateFallbackMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[Highlighter] No usable shader found. Assign a material " +
                               "in the inspector — runtime Shader.Find gets stripped from builds.");
                return null;
            }

            Debug.LogWarning($"[Highlighter] Using runtime shader '{shader.name}'. This can be " +
                             "stripped from a device build — assign a material in the inspector " +
                             "before you deploy.");
            return new Material(shader);
        }
    }
}
