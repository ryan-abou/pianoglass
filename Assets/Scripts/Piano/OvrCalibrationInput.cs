// ---------------------------------------------------------------------------
// RENAME THIS FILE TO  OvrCalibrationInput.cs  AFTER YOU INSTALL THE META XR SDK.
//
// It is shipped with a .txt extension on purpose: it is the only file in
// PianoGlass that references OVRInput, and Unity does not compile .txt files.
// That means the whole project compiles cleanly today, before the SDK is in,
// instead of showing a wall of red "OVRInput could not be found" errors that
// hide any real mistake.
//
// Once the Meta XR All-in-One SDK is installed, rename it and drop it on the
// same GameObject as KeyboardCalibration.
// ---------------------------------------------------------------------------

using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Meta-specific input for calibration: right trigger captures a point,
    /// right trigger while holding the left trigger starts over.
    ///
    /// Keeping this separate is also what makes the project portable. If you
    /// ever move to Unity's OpenXR and the XR Interaction Toolkit, you write a
    /// replacement for this one small file and nothing else changes.
    /// </summary>
    [RequireComponent(typeof(KeyboardCalibration))]
    public class OvrCalibrationInput : MonoBehaviour
    {
        private KeyboardCalibration _calibration;

        private void Awake() => _calibration = GetComponent<KeyboardCalibration>();

        private void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger))
            {
                bool recalibrate = OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger);
                _calibration.OnCaptureButton(recalibrate);
            }
        }
    }
}
