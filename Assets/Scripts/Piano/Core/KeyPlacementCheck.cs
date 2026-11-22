using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Whether the keys are somewhere no real piano can be, seen from the
    /// player's head (#38). Round 11 (2026-11-21): after a recenter the keys
    /// showed up far away and turned about 150 degrees; a fresh calibration
    /// fixed it. Nobody plays a piano that faces away from them, from 3 m
    /// away, or tipped on its side, so any of those means the anchor came
    /// back wrong and the player should set up again. Pure and tested.
    /// </summary>
    public static class KeyPlacementCheck
    {
        /// <summary>Farther than this (sideways, not counting height) is wrong.</summary>
        public const float FarMetres = 3f;
        /// <summary>The keys' front turned more than this away from the head is wrong.</summary>
        public const float FacingAwayDegrees = 100f;
        /// <summary>The key surface tipped more than this from level is wrong.</summary>
        public const float TiltDegrees = 25f;
        /// <summary>Closer than this the head is over the keys and which way
        /// they face it says nothing.</summary>
        public const float OverheadMetres = 0.2f;

        /// <param name="centre">Middle of the keyboard's front edge.</param>
        /// <param name="towardPlayer">The layout's Forward.</param>
        /// <param name="up">The layout's Up.</param>
        /// <param name="head">The head's world position.</param>
        /// <param name="measured">The numbers, for the log, either way.</param>
        public static bool LooksWrong(Vector3 centre, Vector3 towardPlayer, Vector3 up, Vector3 head,
                                      out string measured)
        {
            Vector3 toHead = head - centre;
            toHead.y = 0f;
            Vector3 front = towardPlayer;
            front.y = 0f;
            float away = toHead.magnitude;
            float turned = away >= OverheadMetres && front.sqrMagnitude > 1e-6f ? Vector3.Angle(front, toHead) : 0f;
            float tilt = Vector3.Angle(up, Vector3.up);

            measured = $"{away:0.00} m away, front turned {turned:0} deg from the head, tilted {tilt:0} deg";
            return away > FarMetres || turned > FacingAwayDegrees || tilt > TiltDegrees;
        }
    }
}
