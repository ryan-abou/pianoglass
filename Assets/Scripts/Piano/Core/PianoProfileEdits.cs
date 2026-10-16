using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>What launch found on disk, and whether it can place the keyboard.</summary>
    public readonly struct ProfileCheck
    {
        public readonly bool Valid;
        /// <summary>The active profile when Valid; null otherwise.</summary>
        public readonly PianoProfile Profile;
        /// <summary>Why not, when not Valid. For the log.</summary>
        public readonly string Reason;

        public ProfileCheck(bool valid, PianoProfile profile, string reason)
        {
            Valid = valid; Profile = profile; Reason = reason;
        }
    }

    /// <summary>
    /// The edits anchored calibration makes to a PianoProfileStore, as pure
    /// functions on the in-memory store. The caller loads the file fresh,
    /// applies one of these, and saves it atomically -- never from a copy
    /// held since launch, because ControllerTipCalibration writes the tip
    /// fields into the same file.
    /// </summary>
    public static class PianoProfileEdits
    {
        /// <summary>Launch: localize only an active profile that passes every
        /// check. Anything else is first-time setup -- with the file left as
        /// it is until a replacement saves.</summary>
        public static ProfileCheck CheckForLaunch(ProfileLoadResult loaded)
        {
            if (loaded == null || loaded.Store == null)
                return new ProfileCheck(false, null, loaded?.Reason ?? "no load result");

            PianoProfile p = loaded.Store.ActiveProfile;
            if (p == null)
                return new ProfileCheck(false, null, "no active profile");

            string problem = PianoProfileSerializer.ValidateProfile(p);
            return problem == null
                ? new ProfileCheck(true, p, null)
                : new ProfileCheck(false, null, problem);
        }

        /// <summary>
        /// A fresh two-touch calibration with its new, already-saved anchor.
        /// Replaces the active profile's anchor and geometry in place, or
        /// creates the profile if there is none. <paramref name="local"/> is
        /// anchor-local and already includes any yaw; <paramref name="nudgeKeyboardMm"/>
        /// goes to the store root, where it survives recalibration.
        /// </summary>
        /// <returns>The anchor UUID the profile pointed at before, or null if
        /// there was none (or it was the same one).</returns>
        public static string ApplyCalibration(PianoProfileStore store, string anchorUuid, KeyboardGeometry local,
                                              int lowestNote, int highestNote, float whiteKeyDepth,
                                              Vector3 nudgeKeyboardMm, string utcNow, string newProfileId)
        {
            if (store.profiles == null) store.profiles = new List<PianoProfile>();

            PianoProfile p = store.ActiveProfile;
            if (p == null)
            {
                p = new PianoProfile { profileId = newProfileId, displayName = "Piano" };
                store.profiles.Add(p);
                store.activeProfileId = p.profileId;
            }

            string previous = p.PrimaryAnchorUuid;

            p.anchorUuids = new List<string> { anchorUuid };
            p.lowestNote = lowestNote;
            p.highestNote = highestNote;
            p.whiteKeyDepth = whiteKeyDepth;
            p.SetLocalGeometry(local);
            p.createdUtc = utcNow;
            p.lastVerifiedUtc = "";
            p.lastCorrection = "none";
            store.tipNudgeKeyboardMm = nudgeKeyboardMm;

            return previous == anchorUuid ? null : previous;
        }

        /// <summary>A yaw fine-adjust, baked into the stored geometry. Only
        /// when the active profile still describes <paramref name="anchorUuid"/>:
        /// geometry local to one anchor is meaningless relative to another.</summary>
        public static bool ApplyGeometry(PianoProfileStore store, string anchorUuid, KeyboardGeometry local)
        {
            PianoProfile p = store.ActiveProfile;
            if (p == null || string.IsNullOrEmpty(anchorUuid) || p.PrimaryAnchorUuid != anchorUuid) return false;
            p.SetLocalGeometry(local);
            return true;
        }

        public static void MarkVerified(PianoProfileStore store, string utcNow)
        {
            PianoProfile p = store.ActiveProfile;
            if (p != null) p.lastVerifiedUtc = utcNow;
        }
    }
}
