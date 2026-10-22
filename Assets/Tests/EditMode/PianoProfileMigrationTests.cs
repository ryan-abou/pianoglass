using System;
using System.IO;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>v1 keyboard_calibration.json → the non-spatial parts of v2.</summary>
    public class PianoProfileMigrationTests
    {
        /// <summary>Exactly the shape KeyboardCalibration.Save() writes with
        /// JsonUtility.ToJson(data, true).</summary>
        private const string RealV1 = @"{
    ""low"": {
        ""x"": -0.5512,
        ""y"": 0.7431,
        ""z"": 0.4120
    },
    ""high"": {
        ""x"": 0.6441,
        ""y"": 0.7460,
        ""z"": 0.3611
    },
    ""head"": {
        ""x"": 0.0310,
        ""y"": 1.3021,
        ""z"": -0.1405
    },
    ""nudge"": {
        ""x"": 0.0041,
        ""y"": -0.0012,
        ""z"": -0.0030
    },
    ""lowestNote"": 21,
    ""highestNote"": 108,
    ""whiteKeyDepth"": 0.145
}";

        [Test]
        public void DetectsARealV1File()
        {
            Assert.IsTrue(PianoProfileSerializer.TryParseLegacyV1(RealV1, out LegacyCalibrationV1 v1));
            Assert.AreEqual(21, v1.lowestNote);
            Assert.AreEqual(-0.5512f, v1.low.x, 1e-6f);
        }

        [Test]
        public void DoesNotMistakeAV2FileForV1()
        {
            string v2 = JsonUtility.ToJson(new PianoProfileStore(), true);
            Assert.IsFalse(PianoProfileSerializer.TryParseLegacyV1(v2, out _));
            Assert.IsFalse(PianoProfileSerializer.TryParseLegacyV1("not json", out _));
        }

        [Test]
        public void CarriesNoteRangeAndDepth()
        {
            PianoProfileSerializer.TryParseLegacyV1(RealV1, out LegacyCalibrationV1 v1);
            LegacyCarryOver c = PianoProfileMigration.CarryOver(v1);
            Assert.AreEqual(21, c.LowestNote);
            Assert.AreEqual(108, c.HighestNote);
            Assert.AreEqual(0.145f, c.WhiteKeyDepth, 1e-6f);
        }

        /// <summary>A known keyboard-frame nudge, written into v1 as a world
        /// vector on a keyboard turned 30°, comes back as the same millimetres.</summary>
        [Test]
        public void ConvertsTheWorldNudgeUsingV1sOwnAxesAndHead()
        {
            Quaternion yaw = Quaternion.AngleAxis(30f, Vector3.up);
            Vector3 low = yaw * new Vector3(-0.6f, 0f, 0f) + new Vector3(0f, 0.75f, 0f);
            Vector3 high = yaw * new Vector3(0.6f, 0f, 0f) + new Vector3(0f, 0.75f, 0f);
            Vector3 toward = yaw * Vector3.back;
            Vector3 head = (low + high) * 0.5f + toward * 0.5f + Vector3.up * 0.5f;

            Vector3 wantMm = new Vector3(4f, -1f, 2.5f); // right, up, toward player
            AnchoredKeyboard.Axes(low, high, toward, out Vector3 r, out Vector3 u, out Vector3 f);
            var v1 = new LegacyCalibrationV1
            {
                low = low, high = high, head = head,
                nudge = AnchoredKeyboard.NudgeToWorld(wantMm, r, u, f),
                lowestNote = 21, highestNote = 108, whiteKeyDepth = 0.145f,
            };

            AnchoredKeyboardTests.AssertApprox(wantMm, PianoProfileMigration.CarryOver(v1).TipNudgeKeyboardMm, 1e-3f);
        }

        /// <summary>The carry-over has no positions in it, and a v1 file
        /// whose points were captured somewhere else entirely gives the same
        /// result: nothing spatial is trusted.</summary>
        [Test]
        public void PositionsAreNotUsed()
        {
            PianoProfileSerializer.TryParseLegacyV1(RealV1, out LegacyCalibrationV1 a);
            PianoProfileSerializer.TryParseLegacyV1(RealV1, out LegacyCalibrationV1 b);
            Vector3 elsewhere = new Vector3(3f, 0f, -2f);
            b.low += elsewhere; b.high += elsewhere; b.head += elsewhere;

            LegacyCarryOver ca = PianoProfileMigration.CarryOver(a);
            LegacyCarryOver cb = PianoProfileMigration.CarryOver(b);
            AnchoredKeyboardTests.AssertApprox(ca.TipNudgeKeyboardMm, cb.TipNudgeKeyboardMm, 1e-4f);
        }

        [Test]
        public void LoadingV2LeavesTheV1FileUntouched()
        {
            string dir = Path.Combine(Path.GetTempPath(), "PianoGlassTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string v1Path = Path.Combine(dir, PianoProfileSerializer.LegacyFileName);
                File.WriteAllText(v1Path, RealV1);
                DateTime stamp = File.GetLastWriteTimeUtc(v1Path);

                var ser = new PianoProfileSerializer(Path.Combine(dir, PianoProfileSerializer.DefaultFileName));
                Assert.AreEqual(ProfileLoadStatus.Missing, ser.Load().Status);
                Assert.IsTrue(ser.Save(new PianoProfileStore(), out string err), err);

                Assert.AreEqual(RealV1, File.ReadAllText(v1Path));
                Assert.AreEqual(stamp, File.GetLastWriteTimeUtc(v1Path));
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }
    }
}
