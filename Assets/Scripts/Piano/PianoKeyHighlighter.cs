using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Midi;

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
    /// </summary>
    public class PianoKeyHighlighter : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private KeyboardCalibration calibration;

        [Header("Appearance")]
        [Tooltip("Assign a material made in the editor. Leave empty and one is " +
                 "created at runtime, but a runtime Shader.Find can be stripped " +
                 "from the build — assigning it here is the safe path.")]
        [SerializeField] private Material keyMaterial;

        [Tooltip("Draw every key faintly so you can see whether the calibration " +
                 "still lines up with the real keyboard.")]
        [SerializeField] private bool showIdleKeys = true;

        [SerializeField] private Color idleWhiteKey = new Color(0.55f, 0.60f, 0.50f, 1f);
        [SerializeField] private Color idleBlackKey = new Color(0.16f, 0.18f, 0.14f, 1f);
        [SerializeField] private Color pressedKey = new Color(0.66f, 0.78f, 0.49f, 1f);

        [Tooltip("Height of the key slab, in metres. Thin enough to sit on the real key.")]
        [SerializeField] private float slabThickness = 0.004f;

        [Tooltip("Lift everything slightly so it doesn't z-fight with anything.")]
        [SerializeField] private float surfaceOffset = 0.001f;

        private readonly Dictionary<int, Renderer> _renderers = new Dictionary<int, Renderer>();
        private MaterialPropertyBlock _block;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            // FindAnyObjectByType, not FindFirstObjectByType: the "First" variant
            // is deprecated because it depends on instance-ID ordering, and we
            // only ever expect one calibration in the scene anyway.
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
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

        // ------------------------------------------------------------------

        private void Rebuild(PianoKeyLayout layout)
        {
            foreach (var r in _renderers.Values)
                if (r != null) Destroy(r.gameObject);
            _renderers.Clear();

            if (layout == null) return;

            Material mat = keyMaterial != null ? keyMaterial : CreateFallbackMaterial();

            foreach (var kv in layout.Keys)
            {
                PianoKey key = kv.Value;

                // A thin cube rather than a quad: cubes have no "which way does
                // the normal face" problem, so it either appears or your
                // calibration is wrong — never "it's there but facing away".
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Key_{MidiInput.NoteName(key.Note)}";
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(
                    key.Center + layout.Up * surfaceOffset, key.Rotation);
                go.transform.localScale = new Vector3(key.Width, slabThickness, key.Depth);

                // Colliders would fight with hand/controller physics for no gain.
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                _renderers[key.Note] = renderer;
                SetIdle(key.Note, key.IsBlack);
            }

            Debug.Log($"[Highlighter] built {_renderers.Count} keys");
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
            SetIdle(note, PianoKeyLayout.IsBlackKey(note));
        }

        // ------------------------------------------------------------------

        /// <summary>Public so the note highway and wait-mode can tint keys too.</summary>
        public void SetColor(int note, Color color, bool forceVisible = false)
        {
            if (!_renderers.TryGetValue(note, out var renderer) || renderer == null) return;

            if (forceVisible) renderer.enabled = true;

            renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);   // URP
            _block.SetColor(ColorId, color);       // Built-in, harmless either way
            renderer.SetPropertyBlock(_block);
        }

        public void SetIdle(int note, bool isBlack)
        {
            if (!_renderers.TryGetValue(note, out var renderer) || renderer == null) return;
            renderer.enabled = showIdleKeys;
            SetColor(note, isBlack ? idleBlackKey : idleWhiteKey);
        }

        [ContextMenu("Toggle idle keys")]
        public void ToggleIdleKeys()
        {
            showIdleKeys = !showIdleKeys;
            foreach (var kv in _renderers)
            {
                bool held = MidiInput.Instance != null && MidiInput.Instance.IsHeld(kv.Key);
                if (kv.Value != null) kv.Value.enabled = showIdleKeys || held;
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
