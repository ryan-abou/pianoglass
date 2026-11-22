using System.Collections;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Shows where the Meta button is while the launch prompt asks for a
    /// recenter (#42): someone new, like a judge, doesn't know the button.
    ///
    /// Done the way VR tutorials do it: Meta's own controller model is drawn
    /// over the real right controller as a see-through ghost, and its Meta
    /// button glows and pulses (ControllerButtonGlow). OVRControllerPrefab
    /// picks the model for the headset and lines it up with the real
    /// controller, so the glow is on the real button, not at a guessed spot
    /// (round 12: the first version, a ring at a guessed offset, was about
    /// 1.5 cm off). The controller also buzzes every couple of seconds.
    ///
    /// LaunchResync shows and hides it with the banner. Logs as "[MetaButton]".
    /// </summary>
    public sealed class MetaButtonHint : MonoBehaviour
    {
        private const float BuzzEverySeconds = 2f, BuzzSeconds = 0.12f, BuzzStrength = 0.35f;

        private MetaButtonHintAssets _assets;
        private GameObject _model;
        private ControllerButtonGlow _glow;
        private float _nextBuzz, _buzzEnd;
        private bool _buzzing;

        /// <summary>Built hidden. Null without a camera rig or the assets SceneSetup wires.</summary>
        public static MetaButtonHint Create()
        {
            OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
            var assets = FindAnyObjectByType<MetaButtonHintAssets>();
            if (rig == null || rig.rightControllerAnchor == null || assets == null ||
                assets.controllerPrefab == null || assets.translucent == null)
            {
                Debug.LogWarning("[MetaButton] no rig or assets (run SceneSetup.ConfigureMetaButtonHint): no highlight");
                return null;
            }

            var go = new GameObject(nameof(MetaButtonHint));
            go.SetActive(false);
            go.transform.SetParent(rig.rightControllerAnchor, false);
            var hint = go.AddComponent<MetaButtonHint>();
            hint._assets = assets;
            return hint;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            _nextBuzz = Time.unscaledTime + 0.5f;
            if (_model == null)
            {
                _model = Instantiate(_assets.controllerPrefab, transform, false);
                var helper = _model.GetComponent<OVRControllerHelper>();
                if (helper != null) helper.m_controller = OVRInput.Controller.RTouch;
                StartCoroutine(LightWhenReady());
            }
            Debug.Log("[MetaButton] shown");
        }

        public void Hide()
        {
            StopBuzz();
            gameObject.SetActive(false);
        }

        /// <summary>OVRControllerHelper switches on the right model in its
        /// Start: light it a couple of frames later.</summary>
        private IEnumerator LightWhenReady()
        {
            yield return null;
            yield return null;
            _glow = ControllerButtonGlow.Add(_model, _assets.translucent, _assets.touchPlusRightCap,
                                             ControllerButtonGlow.MetaButtonBones);
            if (_glow == null) Debug.LogWarning("[MetaButton] no Meta button bone on the controller model shown");
        }

        private void OnDisable() => StopBuzz();

        private void Update()
        {
            bool tracked = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) &&
                           OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
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
