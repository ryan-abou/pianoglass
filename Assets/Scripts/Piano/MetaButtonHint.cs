using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Shows where the Meta button is while the launch prompt asks for a
    /// recenter (#42): someone new, like a judge, doesn't know the button.
    /// A ring pulses over the button on the real right controller (seen
    /// through passthrough; the app draws no controller model), and the
    /// controller gives a short buzz every couple of seconds, which points at
    /// the right hand even if the ring is a little off.
    ///
    /// <see cref="ButtonOffset"/> is the button's place in the controller's
    /// own frame. It is a first guess, to be checked by eye on the headset
    /// (round 12). Hidden while the right controller isn't tracked (hands in
    /// use: the prompt also says how to recenter with a palm pinch).
    /// LaunchResync shows and hides it with the banner. Logs as "[MetaButton]".
    /// </summary>
    public sealed class MetaButtonHint : MonoBehaviour
    {
        /// <summary>Metres from the right controller's pose, in its own frame:
        /// on the face, under the thumbstick. A first guess.</summary>
        public static readonly Vector3 ButtonOffset = new Vector3(-0.004f, 0.012f, -0.012f);
        private const float Radius = 0.011f, Width = 0.0025f;
        private const int Points = 32;
        private const float PulseHz = 1.2f;
        private const float BuzzEverySeconds = 2f, BuzzSeconds = 0.12f, BuzzStrength = 0.35f;

        private static readonly Color Glow = new Color(0.45f, 0.85f, 1f, 1f);

        private LineRenderer _ring;
        private float _nextBuzz, _buzzEnd;
        private bool _buzzing;

        /// <summary>Built hidden. Null without a camera rig or a line material to borrow.</summary>
        public static MetaButtonHint Create(Material lineMaterial)
        {
            OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
            if (rig == null || rig.rightControllerAnchor == null || lineMaterial == null) return null;

            var go = new GameObject(nameof(MetaButtonHint));
            go.SetActive(false);
            go.transform.SetParent(rig.rightControllerAnchor, false);
            go.transform.localPosition = ButtonOffset;
            go.transform.localRotation = Quaternion.identity;

            var ring = go.AddComponent<LineRenderer>();
            ring.sharedMaterial = lineMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = Points;
            ring.widthMultiplier = Width;
            ring.numCapVertices = 2;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            // Flat on the controller's face: the face looks along the controller's up.
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius));
            }

            var hint = go.AddComponent<MetaButtonHint>();
            hint._ring = ring;
            return hint;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _nextBuzz = Time.unscaledTime + 0.5f;
            Debug.Log("[MetaButton] ring and buzz on the right controller's Meta button");
        }

        public void Hide()
        {
            StopBuzz();
            gameObject.SetActive(false);
        }

        private void OnDisable() => StopBuzz();

        private void Update()
        {
            bool tracked = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) &&
                           OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
            _ring.enabled = tracked;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * PulseHz * Mathf.PI * 2f);
            Color c = Glow;
            c.a = Mathf.Lerp(0.35f, 1f, pulse);
            _ring.startColor = _ring.endColor = c;
            _ring.widthMultiplier = Width * Mathf.Lerp(0.8f, 1.4f, pulse);

            if (_buzzing && Time.unscaledTime >= _buzzEnd) StopBuzz();
            if (tracked && Time.unscaledTime >= _nextBuzz)
            {
                OVRInput.SetControllerVibration(0.5f, BuzzStrength, OVRInput.Controller.RTouch);
                _buzzing = true;
                _buzzEnd = Time.unscaledTime + BuzzSeconds;
                _nextBuzz = Time.unscaledTime + BuzzEverySeconds;
            }
        }

        private void StopBuzz()
        {
            if (!_buzzing) return;
            _buzzing = false;
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
    }
}
