using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Hands CalibrationFlowController the real, OVRSpatialAnchor-backed
    /// anchor service on the headset, so the controller itself never names a
    /// Meta type. Runs before the first scene loads, i.e. before any Awake.
    /// The editor keeps EditorPianoAnchorService.
    /// </summary>
    public static class OvrAnchorServiceRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (Application.isEditor) return;
            CalibrationFlowController.ServiceFactory = () => new OvrPianoAnchorService();
        }
    }
}
