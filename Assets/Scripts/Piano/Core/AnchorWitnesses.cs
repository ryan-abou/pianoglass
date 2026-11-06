using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Two extra anchors, one at each end of the keyboard, that only watch. The
    /// keys stay on the primary anchor. Each witness remembers where the primary
    /// sat relative to it, so it can say where the primary should be now.
    ///
    /// This measures two things (2026-11-05, the anchor came back 86 mm off after
    /// a day and 21 mm off after 19 minutes):
    ///   * Whether anchors move apart. A witness's prediction against the primary
    ///     does not change when tracking space is re-based, so unlike an anchor's
    ///     world pose it shows the map itself shifting.
    ///   * Whether averaging would have helped: at a Middle C touch, how far each
    ///     one, and all of them together, put the key from where it really is.
    /// </summary>
    public static class AnchorWitnesses
    {
        /// <summary>Remember where the primary sits relative to the witness now.</summary>
        public static void Baseline(WitnessAnchor witness, Pose primary, Pose witnessPose)
        {
            Quaternion inverse = Quaternion.Inverse(witnessPose.rotation);
            witness.primaryInWitness = inverse * (primary.position - witnessPose.position);
            witness.primaryRotationInWitness = inverse * primary.rotation;
        }

        /// <summary>Where this witness says the primary should be.</summary>
        public static Pose PredictPrimary(WitnessAnchor witness, Pose witnessPose) =>
            new Pose(witnessPose.position + witnessPose.rotation * witness.primaryInWitness,
                     witnessPose.rotation * witness.primaryRotationInWitness);
    }
}
