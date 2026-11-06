using System;
using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Scoring;
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
    ///
    /// A wrong or extra press flashes the pressed key coral (F19/F20,
    /// OutcomeTint.WrongKey), in every display mode. It stays a dimmer coral
    /// while held -- never green, which would read as "correct" -- and goes
    /// back to its resting state once released.
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
        ///                Lit keys draw at activeKeysAlpha on
        ///                translucentMaterial, so a hand shows through.
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
        [Tooltip("Source of wrong-note events for the coral pulse. Optional.")]
        [SerializeField] private ScoreKeeper scoreKeeper;

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
                 "OvrKeyOverlayInput (left X) to compare them on the headset. " +
                 "Only used on first launch: after that the last mode chosen " +
                 "is restored from PlayerPrefs.")]
        [SerializeField] private DisplayMode displayMode = DisplayMode.ActiveKeysOnly;

        [Tooltip("Alpha applied to every colour in Translucent mode. Default " +
                 "0.25 -- faint enough to still show a hand through it.")]
        [SerializeField] private float translucentAlpha = 0.25f;

        [Tooltip("Alpha of a lit key (due, pressed or wrong) in ActiveKeysOnly. " +
                 "Less see-through than Translucent so the key still reads as lit, " +
                 "but enough to see a hand through it (Ryan, 2026-11-03).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float activeKeysAlpha = 0.55f;

        /// <summary>The alpha every key colour is drawn with in the current
        /// mode; 1 means the opaque material.</summary>
        private float ModeAlpha => displayMode switch
        {
            DisplayMode.Translucent    => translucentAlpha,
            DisplayMode.ActiveKeysOnly => activeKeysAlpha,
            _                          => 1f,
        };

        [Tooltip("How far ahead of its due time a note's key appears in " +
                 "ActiveKeysOnly/EdgeMarkers. Default 0.5 s.")]
        [SerializeField] private float activeKeyLookaheadSeconds = 0.5f;

        [Tooltip("EdgeMarkers' bar depth, as a fraction of the key's own " +
                 "front-to-back length. Kept small and placed at the far " +
                 "edge so it stays clear of where a finger rests.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] private float edgeMarkerDepthFraction = 0.2f;

        public DisplayMode Mode => displayMode;

        /// <summary>PlayerPrefs key for the last display mode. Stored by
        /// name, not index, so reordering the enum can't silently restore
        /// the wrong mode; an unknown name falls back to the Inspector value.</summary>
        private const string DisplayModePrefKey = "PianoGlass.KeyOverlay.DisplayMode";

        private readonly Dictionary<int, Renderer> _renderers = new Dictionary<int, Renderer>();
        private readonly HashSet<int> _dueSoon = new HashSet<int>();
        /// <summary>Keys pulsing coral, and when the wrong press happened (realtime).</summary>
        private readonly Dictionary<int, float> _wrongAt = new Dictionary<int, float>();
        /// <summary>Wrong keys already released, and when (realtime): fading out.</summary>
        private readonly Dictionary<int, float> _wrongReleasedAt = new Dictionary<int, float>();
        private readonly List<int> _pulseDone = new List<int>();
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
            if (scoreKeeper == null) scoreKeeper = FindAnyObjectByType<ScoreKeeper>();

            // Same hard-coded song NoteHighway uses. Each reads it
            // independently rather than sharing a reference, the same way
            // each independently finds its own KeyboardCalibration -- there
            // is exactly one song right now, so there is nothing to drift.
            _song = SongData.TwinkleTwinkleLittleStar();

            // Launch in whatever mode the player last chose. Restored here,
            // before Rebuild, so the keys are built in it from the start.
            string saved = PlayerPrefs.GetString(DisplayModePrefKey, "");
            if (Enum.TryParse(saved, out DisplayMode restored) && Enum.IsDefined(typeof(DisplayMode), restored))
            {
                displayMode = restored;
                Debug.Log($"[Highlighter] display mode restored: {displayMode}");
            }
        }

        /// <summary>Nothing is drawn at a placement the calibration does not
        /// vouch for -- not even a pressed key. Read live rather than cached,
        /// so the initial state needs no special handling in OnEnable.</summary>
        private bool PlacementValid => calibration != null && calibration.IsPlacementValid;

        private void OnEnable()
        {
            if (calibration != null)
            {
                calibration.Calibrated += Rebuild;
                calibration.PlacementValidChanged += OnPlacementValidChanged;
                if (calibration.IsCalibrated) Rebuild(calibration.Layout);
                if (!PlacementValid) HideAll();
            }

            if (MidiInput.Instance != null)
            {
                MidiInput.Instance.NoteOn += OnNoteOn;
                MidiInput.Instance.NoteOff += OnNoteOff;
            }
            if (scoreKeeper != null) scoreKeeper.WrongNotePlayed += OnWrongNote;
        }

        private void OnDisable()
        {
            if (calibration != null)
            {
                calibration.Calibrated -= Rebuild;
                calibration.PlacementValidChanged -= OnPlacementValidChanged;
            }
            if (MidiInput.Instance != null)
            {
                MidiInput.Instance.NoteOn -= OnNoteOn;
                MidiInput.Instance.NoteOff -= OnNoteOff;
            }
            if (scoreKeeper != null) scoreKeeper.WrongNotePlayed -= OnWrongNote;
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
            if (_renderers.Count == 0 || !PlacementValid) return;
            if (clock != null && (displayMode == DisplayMode.ActiveKeysOnly || displayMode == DisplayMode.EdgeMarkers))
                UpdateDueKeys();
            UpdateWrongPulses();
        }

        private void UpdateDueKeys()
        {
            double from = clock.Loop?.NotesFrom ?? double.NegativeInfinity;
            double until = clock.Loop?.NotesUntil ?? double.PositiveInfinity;
            ActiveNoteSelector.CollectDueSoonPitches(_song.Notes, clock.DisplaySongTime, activeKeyLookaheadSeconds,
                                                     _dueSoon, from, until);

            foreach (var kv in _renderers)
            {
                int note = kv.Key;
                Renderer renderer = kv.Value;
                if (renderer == null) continue;
                if (MidiInput.Instance != null && MidiInput.Instance.IsHeld(note)) continue;
                if (_wrongAt.ContainsKey(note)) continue; // UpdateWrongPulses owns it

                // Memory fade: a lit key would give the note away, so the hint
                // fades with the slab, and is gone when the slab is. A key the
                // player is holding is their own press, and is not touched here.
                float opacity = clock.NoteOpacity;
                bool relevant = _dueSoon.Contains(note) && opacity > 0f;
                renderer.enabled = relevant;
                if (!relevant) continue;

                Color idle = PianoKeyLayout.IsBlackKey(note) ? idleBlackKey : idleWhiteKey;
                if (opacity >= 1f || translucentMaterial == null)
                {
                    renderer.sharedMaterial = MaterialForMode;
                    SetColor(note, idle);
                }
                else
                {
                    renderer.sharedMaterial = translucentMaterial;
                    FadeOut(note, idle, ModeAlpha * opacity);
                }
            }
        }

        /// <summary>
        /// A wrong key flashes coral and stays a steady dimmer coral while
        /// held. Once released it fades OUT as coral -- more transparent
        /// until gone -- never through green or the greenish idle colour,
        /// which read as "that was fine". In FullOverlay/Translucent with
        /// idle keys showing, a key is always drawn, so there it blends to
        /// the idle colour instead. Then the key is handed back.
        /// </summary>
        private void UpdateWrongPulses()
        {
            if (_wrongAt.Count == 0) return;
            float now = Time.realtimeSinceStartup;
            _pulseDone.Clear();

            foreach (var kv in _wrongAt)
            {
                int note = kv.Key;
                Color hue = OutcomeTint.WrongKeyHue(now - kv.Value);

                if (IsHeld(note))
                {
                    _wrongReleasedAt.Remove(note);
                    SetColor(note, hue, forceVisible: true);
                    continue;
                }

                if (!_wrongReleasedAt.TryGetValue(note, out float released))
                    _wrongReleasedAt[note] = released = now;
                float fade = OutcomeTint.WrongReleaseFade(now - released);
                if (fade <= 0f) { _pulseDone.Add(note); continue; }

                if (IdleKeysVisible)
                {
                    Color idle = PianoKeyLayout.IsBlackKey(note) ? idleBlackKey : idleWhiteKey;
                    SetColor(note, Color.Lerp(idle, hue, fade), forceVisible: true);
                }
                else
                {
                    FadeOut(note, hue, ModeAlpha * fade);
                }
            }

            foreach (int note in _pulseDone)
            {
                _wrongAt.Remove(note);
                _wrongReleasedAt.Remove(note);
                if (_renderers.TryGetValue(note, out var renderer) && renderer != null)
                    renderer.sharedMaterial = MaterialForMode;
                ApplyRestState(note, PianoKeyLayout.IsBlackKey(note));
            }
        }

        /// <summary>Whether a key at rest is drawn at all in this mode.</summary>
        private bool IdleKeysVisible =>
            showIdleKeys && (displayMode == DisplayMode.FullOverlay || displayMode == DisplayMode.Translucent);

        /// <summary>Draws one key with an explicit alpha on the transparent
        /// material, even in an otherwise opaque mode (EdgeMarkers), so a
        /// released wrong key can fade out instead of popping off.</summary>
        private void FadeOut(int note, Color color, float alpha)
        {
            if (!_renderers.TryGetValue(note, out var renderer) || renderer == null) return;
            if (translucentMaterial != null) renderer.sharedMaterial = translucentMaterial;
            renderer.enabled = true;
            color.a = alpha;
            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(_block);
        }

        private static bool IsHeld(int note) => MidiInput.Instance != null && MidiInput.Instance.IsHeld(note);

        private void OnWrongNote(WrongNote w) => PulseWrong(w.pressedPitch);

        /// <summary>Pulse a key coral once. Public so wait mode can reuse it.</summary>
        public void PulseWrong(int note)
        {
            if (!PlacementValid || !_renderers.ContainsKey(note)) return;
            _wrongAt[note] = Time.realtimeSinceStartup;
            _wrongReleasedAt.Remove(note);
            // Coral now: ScoreKeeper runs after this component's Update, so
            // waiting for the next pass would show a frame of green first.
            SetColor(note, OutcomeTint.Wrong, forceVisible: true);
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
        private Material MaterialForMode =>
            (ModeAlpha < 1f && translucentMaterial != null) ? translucentMaterial : _opaqueMaterial;

        private void ApplyMaterialForMode()
        {
            Material mat = MaterialForMode;
            if (mat == null) return;

            foreach (var renderer in _renderers.Values)
                if (renderer != null) renderer.sharedMaterial = mat;
        }

        private void OnPlacementValidChanged(bool valid)
        {
            if (!valid)
            {
                HideAll();
                return;
            }

            foreach (var kv in _renderers)
                ApplyRestState(kv.Key, PianoKeyLayout.IsBlackKey(kv.Key));
        }

        private void HideAll()
        {
            foreach (var renderer in _renderers.Values)
                if (renderer != null) renderer.enabled = false;
        }

        private void OnNoteOn(int note, int velocity, double time)
        {
            // Without this, a note-on after Recalibrate() re-showed keys at
            // the old layout's coordinates.
            if (!PlacementValid) return;

            // A fresh press starts clean. If it is wrong too, ScoreKeeper says
            // so later this frame and the coral comes straight back.
            if (_wrongAt.Remove(note))
            {
                _wrongReleasedAt.Remove(note);
                if (_renderers.TryGetValue(note, out var r) && r != null && MaterialForMode != null)
                    r.sharedMaterial = MaterialForMode;
            }

            // Velocity brightens the flash a little — free feedback on touch,
            // and a hint of the dynamics scoring you can add later.
            float t = Mathf.InverseLerp(20f, 110f, velocity);
            SetColor(note, Color.Lerp(pressedKey * 0.7f, pressedKey, t), forceVisible: true);
        }

        private void OnNoteOff(int note, double time)
        {
            if (!_renderers.ContainsKey(note)) return;
            // A wrong key fades out as coral from here (UpdateWrongPulses);
            // resting it now would blink it to idle/hidden for a frame.
            if (_wrongAt.ContainsKey(note))
            {
                _wrongReleasedAt[note] = Time.realtimeSinceStartup;
                return;
            }
            ApplyRestState(note, PianoKeyLayout.IsBlackKey(note));
        }

        // ------------------------------------------------------------------

        /// <summary>Public so the note highway and wait-mode can tint keys too.</summary>
        public void SetColor(int note, Color color, bool forceVisible = false)
        {
            if (!_renderers.TryGetValue(note, out var renderer) || renderer == null) return;

            if (forceVisible && PlacementValid) renderer.enabled = true;

            // Opaque modes ignore alpha; the rest write the mode's alpha so the
            // transparent material shows the hand through the key.
            color = new Color(color.r, color.g, color.b, ModeAlpha);

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

            if (!PlacementValid)
            {
                renderer.enabled = false;
                return;
            }

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

            // Saved on every change, not on quit: a Quest app is usually
            // closed by the system without OnApplicationQuit ever running.
            PlayerPrefs.SetString(DisplayModePrefKey, displayMode.ToString());
            PlayerPrefs.Save();

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
