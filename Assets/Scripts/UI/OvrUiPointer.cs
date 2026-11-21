using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// A ray from the right controller that presses GlassButtons with the
    /// right trigger. Only there while a button is pressable, so it is gone
    /// during play. A stopgap until hand poke arrives in Phase 4.
    ///
    /// The right trigger also belongs to OvrCalibrationInput, so it yields
    /// the trigger only while the ray is actually on a button
    /// (<see cref="OwnsTrigger"/>); anywhere else the trigger does what it
    /// always did. The only OVRInput here; GlassButton is Meta-free.
    ///
    /// On a panel's glass (not a button) the trigger is PanelGrabber's: held,
    /// it moves the panel (Ryan, 2026-11-20). This hands it the ray and the
    /// trigger each frame.
    /// </summary>
    public class OvrUiPointer : MonoBehaviour
    {
        [Tooltip("The rig whose right controller anchor the ray starts from. Assigned by SceneSetup.")]
        [SerializeField] private OVRCameraRig rig;
        [Tooltip("Draws the ray. Assigned by SceneSetup.")]
        [SerializeField] private LineRenderer line;
        [Tooltip("Metres: how far the ray is drawn when it hits nothing.")]
        [SerializeField] private float reach = 2f;

        /// <summary>The ray is on a button: its trigger pull is for the button.</summary>
        public static bool OwnsTrigger { get; private set; }

        private GlassButton _hovered;

        private static bool _hasRay, _trigger, _triggerDown, _onButton;
        private static Ray _ray;

        /// <summary>The right controller's ray this frame, its trigger (held,
        /// and pulled this frame) and whether the ray is on a button. False
        /// with no controller in hand.</summary>
        public static bool TryControllerRay(out Ray ray, out bool trigger, out bool down, out bool onButton)
        {
            ray = _ray;
            trigger = _trigger;
            down = _triggerDown;
            onButton = _onButton;
            return _hasRay;
        }

        private void Update()
        {
            GlassButton hit = null;
            float nearest = float.PositiveInfinity;
            bool anyPressable = false;
            Ray ray = default;

            // Hands tracked: the hand ray (PanelGrabber) is the pointer, and a
            // pinch would read as this trigger.
            if (rig != null && rig.rightControllerAnchor != null && !PianoGlass.Piano.ControllerInput.Ignored &&
                OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
            {
                ray = new Ray(rig.rightControllerAnchor.position, rig.rightControllerAnchor.forward);
                foreach (GlassButton b in GlassButton.All)
                {
                    if (!b.IsPressable || PanelGrabber.ControllerHolding) continue; // the held panel's own buttons
                    if (!b.AlwaysShown) anyPressable = true;
                    if (b.TryHit(ray, out float d) && d < nearest)
                    {
                        nearest = d;
                        hit = b;
                    }
                }
            }

            if (_hovered != hit)
            {
                if (_hovered != null) _hovered.SetHovered(false);
                if (hit != null) hit.SetHovered(true);
                _hovered = hit;
            }
            OwnsTrigger = hit != null || PanelGrabber.ControllerOnPanel;
            _hasRay = ray.direction != Vector3.zero;
            _ray = ray;
            _onButton = hit != null;
            _trigger = _hasRay && OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
            _triggerDown = _hasRay && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);

            if (line != null)
            {
                bool onPanel = hit == null && _hasRay && PanelGrabber.ControllerOnPanel;
                bool show = anyPressable || hit != null || onPanel;
                line.enabled = show;
                if (show)
                {
                    line.SetPosition(0, ray.origin);
                    line.SetPosition(1, onPanel ? PanelGrabber.ControllerRayEnd : ray.GetPoint(hit != null ? nearest : reach));
                }
            }

            if (hit != null && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                hit.Press();
        }

        private void OnDisable()
        {
            OwnsTrigger = false;
            _hasRay = _trigger = _triggerDown = _onButton = false;
            if (_hovered != null) _hovered.SetHovered(false);
            _hovered = null;
            if (line != null) line.enabled = false;
        }
    }
}
