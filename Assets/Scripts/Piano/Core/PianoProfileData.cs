using System;
using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Root of piano_profiles.json, schema v2.
    ///
    /// JsonUtility-shaped on purpose: public fields, no dictionaries, no
    /// System.Guid (UUIDs are "D"-format strings). Everything spatial in a
    /// profile is anchor-local. No anchor's world position is ever stored --
    /// that is the number a recenter invalidates.
    ///
    /// The controller-tip fields live here at the root, not in a profile,
    /// because they belong to the controller, not to the piano: the same tip
    /// offset is right at every keyboard.
    /// </summary>
    [Serializable]
    public class PianoProfileStore
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string activeProfileId = "";

        /// <summary>Fine trim on top of the tip offset, millimetres along
        /// (right, up, toward player). Survives recalibration and heading
        /// changes.</summary>
        public Vector3 tipNudgeKeyboardMm;

        /// <summary>What a fresh calibration starts with once the tip is
        /// measured: the nudge the player settled on straight after the last
        /// one, before confirming. Touches land a steady few millimetres off
        /// (2026-11-05), so this is hardware, unlike a nudge made later, which
        /// may only be following a drifted anchor. Absent in older files, which
        /// JsonUtility reads as zero.</summary>
        public Vector3 calibrationBiasMm;

        /// <summary>Set by pivot calibration. When false, tipOffsetController
        /// is meaningless and PointerTip stays where the scene put it.</summary>
        public bool hasTipOffset;
        /// <summary>Tip position in the right controller's local frame, metres.</summary>
        public Vector3 tipOffsetController;
        public float tipOffsetRmsMm;
        public string tipOffsetUtc = "";

        public List<PianoProfile> profiles = new List<PianoProfile>();

        public PianoProfile FindProfile(string profileId)
        {
            if (profiles == null || string.IsNullOrEmpty(profileId)) return null;
            foreach (var p in profiles)
                if (p != null && p.profileId == profileId) return p;
            return null;
        }

        public PianoProfile ActiveProfile => FindProfile(activeProfileId);
    }

    /// <summary>One physical piano, as seen from its anchor.</summary>
    [Serializable]
    public class PianoProfile
    {
        public string profileId = "";
        public string displayName = "";

        /// <summary>A list so a two-anchor mode (one near each end) can be
        /// added without a schema change. Today exactly one.</summary>
        public List<string> anchorUuids = new List<string>();

        public int lowestNote;
        public int highestNote;
        public float whiteKeyDepth;

        /// <summary>Front-edge centres of the lowest and highest keys, and
        /// the unit horizontal direction toward the player -- all in the
        /// anchor's local frame. Facing is stored, not inferred from the head
        /// at load time, so the keyboard can never come back mirrored.</summary>
        public Vector3 lowFrontLocal;
        public Vector3 highFrontLocal;
        public Vector3 towardPlayerLocal;

        /// <summary>When true, MIDI identifies which key the tip touched.
        /// Off by default: the calibrated piano is not always the MIDI one.</summary>
        public bool midiMatchesPiano;

        public string createdUtc = "";
        public string lastVerifiedUtc = "";
        /// <summary>"none", "middle-c" or "two-point".</summary>
        public string lastCorrection = "none";
        /// <summary>The shift the last correction applied, millimetres in
        /// the anchor's frame (y is world up). A record only: it is already
        /// baked into the front-edge points above. Absent in older files,
        /// which JsonUtility reads as zero.</summary>
        public Vector3 lastCorrectionMm;

        public KeyboardGeometry LocalGeometry => new KeyboardGeometry(lowFrontLocal, highFrontLocal, towardPlayerLocal);

        public void SetLocalGeometry(KeyboardGeometry g)
        {
            lowFrontLocal = g.LowFront;
            highFrontLocal = g.HighFront;
            towardPlayerLocal = g.TowardPlayer;
        }

        public string PrimaryAnchorUuid =>
            anchorUuids != null && anchorUuids.Count > 0 ? anchorUuids[0] : null;
    }

    /// <summary>keyboard_calibration.json as KeyboardCalibration has always
    /// written it: raw tracking-space points, no version field. Read only to
    /// carry over the parts that are not spatial; never trusted for position.</summary>
    [Serializable]
    public class LegacyCalibrationV1
    {
        public Vector3 low;
        public Vector3 high;
        public Vector3 head;
        public Vector3 nudge;
        public int lowestNote;
        public int highestNote;
        public float whiteKeyDepth;
    }

    /// <summary>What survives from a v1 file. Deliberately no points.</summary>
    public readonly struct LegacyCarryOver
    {
        public readonly int LowestNote;
        public readonly int HighestNote;
        public readonly float WhiteKeyDepth;
        /// <summary>The v1 world nudge, re-expressed in the keyboard frame
        /// using v1's own axes and its saved head for facing.</summary>
        public readonly Vector3 TipNudgeKeyboardMm;

        public LegacyCarryOver(int lowestNote, int highestNote, float whiteKeyDepth, Vector3 tipNudgeKeyboardMm)
        {
            LowestNote = lowestNote; HighestNote = highestNote;
            WhiteKeyDepth = whiteKeyDepth; TipNudgeKeyboardMm = tipNudgeKeyboardMm;
        }
    }

    public static class PianoProfileMigration
    {
        /// <summary>
        /// v1 → the non-spatial parts of v2. A world nudge cannot be trusted
        /// for position after a recenter, but its components *relative to the
        /// keyboard v1 described* can: that conversion is rotation-invariant.
        /// This is the first time v1's saved head is ever read.
        /// </summary>
        public static LegacyCarryOver CarryOver(LegacyCalibrationV1 v1)
        {
            AnchoredKeyboard.Axes(v1.low, v1.high, v1.head - v1.low,
                                  out Vector3 right, out Vector3 up, out Vector3 forward);
            Vector3 nudgeMm = AnchoredKeyboard.NudgeToKeyboardMm(v1.nudge, right, up, forward);
            return new LegacyCarryOver(v1.lowestNote, v1.highestNote, v1.whiteKeyDepth, nudgeMm);
        }
    }
}
