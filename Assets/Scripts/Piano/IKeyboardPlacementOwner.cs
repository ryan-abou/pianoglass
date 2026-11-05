using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Something other than KeyboardCalibration's own captured points decides
    /// where the keyboard is -- today, CalibrationFlowController placing it
    /// from a spatial anchor. While one is set, KeyboardCalibration still
    /// captures the two touches and still owns Layout / IsPlacementValid and
    /// their events (so every overlay works unchanged), but hands the
    /// decisions and the fine-adjust to the owner, and neither loads nor
    /// writes keyboard_calibration.json.
    /// </summary>
    public interface IKeyboardPlacementOwner
    {
        /// <summary>What to tell the player while KeyboardCalibration is in
        /// Stage.Waiting (localizing, saving).</summary>
        string WaitingPrompt { get; }

        /// <summary>A Middle C fix is under way: the player has instructions to read.</summary>
        bool IsCorrecting { get; }

        /// <summary>Both keys touched. <paramref name="captured"/> is the raw
        /// layout -- no nudge, no yaw -- with facing from the head.</summary>
        void OnPointsCaptured(PianoKeyLayout captured);

        void OnRecalibrateRequested();

        /// <summary>The capture button pressed while waiting: retry, if the
        /// owner is offering one.</summary>
        void OnTriggerWhileWaiting();

        /// <summary>Metres along (right, up, toward player).</summary>
        void Nudge(Vector3 deltaKeyboardMetres);
        void AdjustYaw(float deltaDegrees);
        void ClearNudge(string reason);

        /// <summary>Sticks at rest: write the adjustment.</summary>
        void Persist();

        Vector3 NudgeMillimetres { get; }
        float YawDegrees { get; }

        /// <summary>The prompt while the keyboard is placed (Stage.Calibrated),
        /// or null for KeyboardCalibration's own.</summary>
        string PlacedPrompt { get; }

        /// <summary>The placement is on show and waiting for the player to
        /// say it looks right.</summary>
        bool AwaitingConfirmation { get; }

        /// <summary>A pressed while AwaitingConfirmation.</summary>
        void Confirm();

        /// <summary>Right grip: start or cancel a Middle C fix.</summary>
        void ToggleCorrection();

        /// <summary>The capture button pressed while placed, with the tip's
        /// world position -- a Middle C touch, if one is in progress.</summary>
        void OnTriggerWhilePlaced(Vector3 tipWorld);
    }
}
