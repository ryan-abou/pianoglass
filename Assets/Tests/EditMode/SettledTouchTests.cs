using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class SettledTouchTests
    {
        [Test]
        public void TheMiddleOfEachAxisIsTakenOnItsOwn()
        {
            var points = new List<Vector3>
            {
                new Vector3(0.10f, 0.72f, 0.47f),
                new Vector3(0.11f, 0.71f, 0.45f),
                new Vector3(0.09f, 0.73f, 0.46f),
            };
            Vector3 m = SettledTouch.Median(points);
            Assert.AreEqual(0.10f, m.x, 1e-6f);
            Assert.AreEqual(0.72f, m.y, 1e-6f);
            Assert.AreEqual(0.46f, m.z, 1e-6f);
        }

        [Test]
        public void OneWildFrameDoesNotMoveIt()
        {
            var points = new List<Vector3>();
            for (int i = 0; i < 9; i++) points.Add(new Vector3(0.5f, 0.7f, 0.4f + (i % 3 - 1) * 0.001f));
            points.Add(new Vector3(0.5f, 0.75f, 0.4f)); // a 5 cm jump on one frame
            Vector3 m = SettledTouch.Median(points);
            Assert.AreEqual(0.7f, m.y, 1e-6f);
            Assert.AreEqual(0.05f, SettledTouch.Spread(points, m), 1e-4f);
        }

        [Test]
        public void AnEvenCountTakesTheTwoMiddleValues()
        {
            var points = new List<Vector3> { Vector3.zero, Vector3.one, new Vector3(2f, 2f, 2f), new Vector3(9f, 9f, 9f) };
            Assert.AreEqual(new Vector3(1.5f, 1.5f, 1.5f), SettledTouch.Median(points));
        }

        [Test]
        public void NoFramesGiveZero()
        {
            Assert.AreEqual(Vector3.zero, SettledTouch.Median(new List<Vector3>()));
        }
    }
}
