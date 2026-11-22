using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class KeyPlacementCheckTests
    {
        // Keys along +X at the origin, front toward -Z, where the player sits.
        private static readonly Vector3 Front = Vector3.back;
        private static readonly Vector3 Seated = new Vector3(0.1f, 0.45f, -0.5f);

        [Test]
        public void APlayerAtThePianoIsFine()
        {
            Assert.IsFalse(KeyPlacementCheck.LooksWrong(Vector3.zero, Front, Vector3.up, Seated, out string m));
            StringAssert.Contains("0.51 m away", m);
        }

        [Test]
        public void KeysTurned150DegreesLookWrong()
        {
            // Round 11: the keys came back turned about 150 degrees.
            Vector3 turned = Quaternion.Euler(0f, 150f, 0f) * Front;
            Assert.IsTrue(KeyPlacementCheck.LooksWrong(Vector3.zero, turned, Vector3.up, Seated, out _));
        }

        [Test]
        public void KeysAcrossTheRoomLookWrong()
        {
            Assert.IsTrue(KeyPlacementCheck.LooksWrong(new Vector3(0f, 0f, 3.5f), Front, Vector3.up,
                                                       new Vector3(0f, 0.45f, -0.4f), out _));
        }

        [Test]
        public void TippedKeysLookWrong()
        {
            Vector3 up = Quaternion.Euler(0f, 0f, 40f) * Vector3.up;
            Assert.IsTrue(KeyPlacementCheck.LooksWrong(Vector3.zero, Front, up, Seated, out _));
        }

        [Test]
        public void StandingToTheSideOrLeaningOverIsFine()
        {
            // Reaching for C8 from the end of the bench: 80 degrees to the side.
            Vector3 side = new Vector3(0.6f, 0.5f, -0.1f);
            Assert.IsFalse(KeyPlacementCheck.LooksWrong(Vector3.zero, Front, Vector3.up, side, out _));
            // Head right over the keys: which way they face says nothing.
            Vector3 over = new Vector3(0f, 0.4f, 0.1f);
            Assert.IsFalse(KeyPlacementCheck.LooksWrong(Vector3.zero, Front, Vector3.up, over, out _));
        }
    }
}
