using System.Collections;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Shows where the Meta button is while the launch prompt asks for a
    /// recenter (#42): someone new, like a judge, doesn't know the button.
    ///
    /// Meta's own controller model is lined up with the real right controller
    /// but not drawn; only its Meta button glows and pulses, on top of the
    /// real button (ControllerButtonGlow; round 13: just the button, no
    /// model, Ryan). OVRControllerPrefab
    /// picks the model for the headset and lines it up with the real
    /// controller, so the glow is on the real button, not at a guessed spot
    /// (round 12: the first version, a ring at a guessed offset, was about
    /// 1.5 cm off). The controller ticks faintly with each brightening of the
    /// glow, and buzzes once, harder, when the recenter happens (round 15;
    /// it has one motor, so it is the whole controller, not the button).
    ///
    /// LaunchResync shows and hides it with the banner. Logs as "[MetaButton]".
    /// </summary>
    public sealed class MetaButtonHint : MonoBehaviour
    {
        /// <summary>A tick with each glow peak: faint, but there (round 15; was
        /// 0.35 for 0.12 s every 2 s). Ryan says on the headset if it's too faint.</summary>
        private const float TickStrength = 0.2f, TickSeconds = 0.06f;
        /// <summary>The recenter happened: one clear buzz.</summary>
        private const float DoneStrength = 0.7f, DoneSeconds = 0.2f;

        private MetaButtonHintAssets _assets;
        private GameObject _model;
        private ControllerButtonGlow _glow;
        private float _buzzEnd;
        private bool _buzzing, _done;
        /// <summary>The last glow peak ticked, counted from launch.</summary>
        private long _lastPeak;

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
            _done = false;
            _lastPeak = Peak(Time.unscaledTime);
            if (_model != null) _model.SetActive(true);
            if (_model == null)
            {
                _model = Instantiate(_assets.controllerPrefab, transform, false);
                var helper = _model.GetComponent<OVRControllerHelper>();
                if (helper != null) helper.m_controller = OVRInput.Controller.RTouch;
            }
            // Hiding stops the coroutine: start it again if it hadn't lit yet.
            if (_glow == null) StartCoroutine(LightWhenReady());
            Debug.Log("[MetaButton] shown");
        }

        public void Hide()
        {
            StopBuzz();
            gameObject.SetActive(false);
        }

        /// <summary>The recenter happened: the glow goes at once, one clear
        /// buzz, then it is hidden.</summary>
        public void Done()
        {
            if (!gameObject.activeSelf) return;
            if (_model != null) _model.SetActive(false);
            _done = true;
            Buzz(DoneStrength, DoneSeconds);
        }

        /// <summary>Tries again until OVRControllerHelper has switched the right
        /// model on: it isn't always ready a couple of frames in (round 13: no
        /// glow on 2 of 5 launches, when that was all it waited).</summary>
        private IEnumerator LightWhenReady()
        {
            float started = Time.unscaledTime;
            while (_glow == null)
            {
                yield return new WaitForSecondsRealtime(RetrySeconds);
                _glow = ControllerButtonGlow.Add(_model, _assets.translucent, _assets.touchPlusRightCap,
                                                 ControllerButtonGlow.MetaButtonBones);
                if (_glow == null && Time.unscaledTime - started > GiveUpSeconds)
                {
                    Debug.LogWarning($"[MetaButton] no Meta button bone on the controller model after {GiveUpSeconds:0} s");
                    yield break;
                }
            }
            Debug.Log($"[MetaButton] glowing {Time.unscaledTime - started:0.0} s after the prompt");
        }

        private const float RetrySeconds = 0.1f, GiveUpSeconds = 30f;

        private void OnDisable() => StopBuzz();

        private void Update()
        {
            float now = Time.unscaledTime;
            if (_buzzing && now >= _buzzEnd)
            {
                StopBuzz();
                if (_done)
                {
                    Hide();
                    return;
                }
            }
            if (_done) return;

            // A faint tick each time the glow is brightest.
            long peak = Peak(now);
            if (peak == _lastPeak) return;
            _lastPeak = peak;
            bool tracked = OVRInput.IsControllerConnected(OVRInput.Controller.RTouch) &&
                           OVRInput.GetControllerPositionTracked(OVRInput.Controller.RTouch);
            if (tracked && _glow != null) Buzz(TickStrength, TickSeconds);
        }

        /// <summary>How many glow peaks there have been by <paramref name="time"/>
        /// (ControllerButtonGlow.Pulse is brightest a quarter of the way through each).</summary>
        private static long Peak(float time) =>
            (long)System.Math.Floor(time * ControllerButtonGlow.PulseHz - 0.25);

        private void Buzz(float strength, float seconds)
        {
            OVRInput.SetControllerVibration(0.5f, strength, OVRInput.Controller.RTouch);
            _buzzing = true;
            _buzzEnd = Time.unscaledTime + seconds;
        }

        private void StopBuzz()
        {
            if (!_buzzing) return;
            _buzzing = false;
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }
    }
}
