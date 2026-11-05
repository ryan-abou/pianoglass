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

        private void Update()
        {
            GlassButton hit = null;
            float nearest = float.PositiveInfinity;
            bool anyPressable = false;
            Ray ray = default;

            if (rig != null && rig.rightControllerAnchor != null &&
                OVRInput.IsControllerConnected(OVRInput.Controller.RTouch))
            {
                ray = new Ray(rig.rightControllerAnchor.position, rig.rightControllerAnchor.forward);
                foreach (GlassButton b in GlassButton.All)
                {
                    if (!b.IsPressable) continue;
                    anyPressable = true;
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
            OwnsTrigger = hit != null;

            if (line != null)
            {
                line.enabled = anyPressable;
                if (anyPressable)
                {
                    line.SetPosition(0, ray.origin);
                    line.SetPosition(1, ray.GetPoint(hit != null ? nearest : reach));
                }
            }

            if (hit != null && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
                hit.Press();
        }

        private void OnDisable()
        {
            OwnsTrigger = false;
            if (_hovered != null) _hovered.SetHovered(false);
            _hovered = null;
            if (line != null) line.enabled = false;
        }
    }
}
