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
            p.lastCorrectionMm = Vector3.zero;
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

        /// <summary>A one- or two-point correction: the corrected geometry
        /// plus a record of what was applied. Same anchor rule as ApplyGeometry.</summary>
        public static bool ApplyCorrection(PianoProfileStore store, string anchorUuid, KeyboardGeometry local,
                                           string kind, Vector3 appliedLocalMm)
        {
            if (!ApplyGeometry(store, anchorUuid, local)) return false;
            PianoProfile p = store.ActiveProfile;
            p.lastCorrection = kind;
            p.lastCorrectionMm = appliedLocalMm;
            return true;
        }

        public static void MarkVerified(PianoProfileStore store, string utcNow)
        {
            PianoProfile p = store.ActiveProfile;
            if (p != null) p.lastVerifiedUtc = utcNow;
        }

        /// <summary>Where the right Quest 3 controller's tip is, in its own
        /// frame, metres: Ryan's pivot calibration (RMS 1.3 mm, 2026-11-14).
        /// Every Quest 3 controller is the same shape, so a new install starts
        /// here instead of at the tracked origin, which is centimetres away.</summary>
        public static readonly Vector3 BuiltInTipOffsetMetres = new Vector3(-0.0175f, -0.0336f, 0.0286f);

        /// <summary>The controller calibration bias a new install starts with:
        /// the average of Ryan's last three on the Quest 3 (round 10,
        /// 2026-11-21), up 15.7 mm and 10.2 mm toward the player, no sideways.
        /// It counts as one calibration, so a tester's own nudges take over.</summary>
        public static readonly Vector3 BuiltInControllerBiasMm = new Vector3(0f, 15.7f, 10.2f);

        public const string BuiltInTipUtc = "built-in";

        /// <summary>A store with no measured tip gets the built-in tip, and the
        /// built-in bias if it has never learned one. A tester has no way to
        /// measure the tip (it is a dev-only control). Returns false, and
        /// changes nothing, when the tip is already there.</summary>
        public static bool SeedControllerDefaults(PianoProfileStore store)
        {
            if (store == null || store.hasTipOffset) return false;
            store.hasTipOffset = true;
            store.tipOffsetController = BuiltInTipOffsetMetres;
            store.tipOffsetRmsMm = 0f;
            store.tipOffsetUtc = BuiltInTipUtc;
            if (store.calibrationBiasMm == Vector3.zero && store.calibrationBiasSamples == 0)
            {
                store.calibrationBiasMm = BuiltInControllerBiasMm;
                store.calibrationBiasSamples = 1;
            }
            return true;
        }
    }
}
