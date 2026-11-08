using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PianoGlass.UI
{
    /// <summary>
    /// A mouse click in the Game view presses the glass button under it, so
    /// the menus can be used at the desk. OvrUiPointer needs a real controller.
    /// Uses OnGUI for the same reason MidiEditorSimulator does: it works
    /// whichever input system the project is set to.
    ///
    /// Editor only: it compiles to nothing in device builds.
    /// </summary>
    public class EditorUiPointer : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnGUI()
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            // OnGUI counts down from the top of the screen.
            Ray ray = camera.ScreenPointToRay(new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0f));
            GlassButton hit = null;
            float nearest = float.PositiveInfinity;
            foreach (GlassButton button in GlassButton.All)
            {
                if (!button.IsPressable || HandledByEventSystem(button)) continue;
                if (button.TryHit(ray, out float distance) && distance < nearest)
                {
                    nearest = distance;
                    hit = button;
                }
            }

            if (hit != null) hit.Press();
        }

        /// <summary>The results card has a raycaster, so a working event
        /// system already gives its buttons the click.</summary>
        private static bool HandledByEventSystem(GlassButton button) =>
            EventSystem.current != null && EventSystem.current.currentInputModule != null &&
            button.GetComponentInParent<GraphicRaycaster>() != null;
#endif
    }
}
