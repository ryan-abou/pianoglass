using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// Pivot calibration of the controller tip, ending with the regression
    /// case for the rotation problem: a 30° wrist difference between the A0
    /// and C8 touches turns an unmodelled tip offset into about a degree of
    /// keyboard yaw, and the solved offset makes it vanish.
    /// </summary>
    public class PivotCalibrationTests
    {
        private static readonly Vector3 Pivot = new Vector3(0.2f, 0.78f, 0.35f);

        /// <summary>Rock the controller in a cone around the pivot: tilt
        /// about two axes, plus some twist, the way a hand actually does it.</summary>
        private static List<Pose> Rock(Vector3 tip, float amplitudeDegrees, int count = 120,
                                       float noiseMetres = 0f, int seed = 7, bool singleAxis = false)
        {
            var rng = new System.Random(seed);
            var poses = new List<Pose>(count);
            Quaternion baseRot = Quaternion.Euler(-35f, 10f, 0f); // tip pointing down-forward

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count * Mathf.PI * 2f;
                Quaternion wobble = singleAxis
                    ? Quaternion.AngleAxis(amplitudeDegrees * Mathf.Sin(t * 3f), Vector3.up)
                    : Quaternion.Euler(amplitudeDegrees * Mathf.Sin(t),
                                       amplitudeDegrees * 0.6f * Mathf.Sin(t * 2f),
                                       amplitudeDegrees * Mathf.Cos(t));
                Quaternion r = baseRot * wobble;

                Vector3 noise = Vector3.zero;
                if (noiseMetres > 0f)
                {
                    noise = new Vector3((float)(rng.NextDouble() * 2 - 1),
                                        (float)(rng.NextDouble() * 2 - 1),
                                        (float)(rng.NextDouble() * 2 - 1)).normalized * noiseMetres;
                }

                // The tip stays on the pivot: c = p − R·t.
                poses.Add(new Pose(Pivot - r * tip + noise, r));
            }
            return poses;
        }

        [TestCase(0.0f, -0.02f, 0.035f)]  // ~4 cm, below and ahead of the tracked origin
        [TestCase(0.01f, -0.03f, 0.04f)]  // ~5 cm
        [TestCase(0.0f, 0.0f, 0.03f)]     // 3 cm straight ahead
        public void RecoversTheTipOffsetToUnderHalfAMillimetre(float x, float y, float z)
        {
            Vector3 tip = new Vector3(x, y, z);
            PivotResult r = PivotCalibration.Solve(Rock(tip, 30f));

            Assert.IsTrue(r.Success, r.Error);
            Assert.Less(Vector3.Distance(tip, r.TipOffset), 0.0005f);
            Assert.Less(Vector3.Distance(Pivot, r.Pivot), 0.0005f);
            Assert.Less(r.RmsMetres, 0.0001f);
        }

        [Test]
        public void ReportsRmsCorrectlyWithOneMillimetreOfNoise()
        {
            Vector3 tip = new Vector3(0f, -0.02f, 0.035f);
            PivotResult r = PivotCalibration.Solve(Rock(tip, 30f, count: 300, noiseMetres: 0.001f));

            Assert.IsTrue(r.Success, r.Error);
            // Every sample is exactly 1 mm off; the fit absorbs a little of it.
            Assert.That(r.RmsMetres, Is.InRange(0.0007f, 0.00105f));
            Assert.Less(Vector3.Distance(tip, r.TipOffset), 0.001f);
        }

        [Test]
        public void RejectsTooLittleRotation()
        {
            PivotResult r = PivotCalibration.Solve(Rock(new Vector3(0f, -0.02f, 0.035f), 5f));
            Assert.IsFalse(r.Success);
            Assert.Less(r.SpreadDegrees, PivotCalibration.DefaultMinSpreadDegrees);
        }

        [Test]
        public void RejectsATwistAboutOneAxisOnly()
        {
            PivotResult r = PivotCalibration.Solve(Rock(new Vector3(0f, -0.02f, 0.035f), 40f, singleAxis: true));
            Assert.IsFalse(r.Success, "one component of the offset is undetermined by a single-axis twist");
            Assert.Greater(r.SpreadDegrees, PivotCalibration.DefaultMinSpreadDegrees);
        }

        [Test]
        public void RejectsTooFewSamples()
        {
            Assert.IsFalse(PivotCalibration.Solve(Rock(Vector3.forward * 0.04f, 30f, count: 5)).Success);
            Assert.IsFalse(PivotCalibration.Solve(null).Success);
        }

        // ------------------------------------------------------------------
        // The regression case
        // ------------------------------------------------------------------

        /// <summary>
        /// The tip is 4 cm below the tracked origin. Reaching A0 the wrist is
        /// pitched 15° one way, reaching C8 15° the other -- 30° apart. With
        /// PointerTip at the origin (today) the captured points disagree by
        /// about 2 cm front-to-back, which is about a degree of yaw over the
        /// keyboard: the error that nudging never removes. With the solved
        /// offset it disappears.
        /// </summary>
        [Test]
        public void ThirtyDegreeWristDifferenceCausesYawThatTheSolvedOffsetRemoves()
        {
            Vector3 tipTrue = new Vector3(0f, -0.04f, 0f);
            float span = (PianoKeyLayout.CountWhiteKeys(21, 108) - 1) * PianoKeyLayout.StandardWhiteKeySpacing;
            Vector3 a0 = new Vector3(-span / 2f, 0.75f, 0.3f);
            Vector3 c8 = new Vector3(span / 2f, 0.75f, 0.3f);

            Quaternion atA0 = Quaternion.AngleAxis(-15f, Vector3.right);
            Quaternion atC8 = Quaternion.AngleAxis(15f, Vector3.right);
            var poseA0 = new Pose(a0 - atA0 * tipTrue, atA0);
            var poseC8 = new Pose(c8 - atC8 * tipTrue, atC8);

            float YawError(Vector3 tipModel)
            {
                Vector3 capturedA0 = PivotCalibration.TipPosition(poseA0, tipModel);
                Vector3 capturedC8 = PivotCalibration.TipPosition(poseC8, tipModel);
                return Mathf.Abs(AnchoredKeyboard.YawBetween(c8 - a0, capturedC8 - capturedA0));
            }

            float today = YawError(Vector3.zero);
            Assert.Greater(today, 0.5f, "an unmodelled 4 cm tip offset gives visible yaw");
            Assert.Less(today, 2.5f);

            PivotResult solved = PivotCalibration.Solve(Rock(tipTrue, 30f, noiseMetres: 0.0005f));
            Assert.IsTrue(solved.Success, solved.Error);

            float fixedYaw = YawError(solved.TipOffset);
            Assert.Less(fixedYaw, 0.05f, "the solved offset removes it");
        }
    }
}
