using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>The witness anchors at each end of the keyboard: they measure,
    /// and the keys stay on the primary.</summary>
    public class AnchorWitnessTests
    {
        private const int Lowest = 21, Highest = 108;
        private const float Depth = 0.145f;
        private static readonly float Span = (PianoKeyLayout.CountWhiteKeys(Lowest, Highest) - 1) * PianoKeyLayout.StandardWhiteKeySpacing;
        private static Vector3 Low => new Vector3(-Span / 2f, 0.75f, 0.3f);
        private static Vector3 High => new Vector3(Span / 2f, 0.75f, 0.3f);
        private static readonly Vector3 Toward = Vector3.back;
        private static readonly DateTime Now = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

        private string _dir, _path;
        private EditorPianoAnchorService _fake;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PianoGlassWitness_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, PianoProfileSerializer.DefaultFileName);
            _fake = new EditorPianoAnchorService();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private AnchoredCalibrationSession NewSession(bool witnesses = true) =>
            new AnchoredCalibrationSession(_fake, new PianoProfileSerializer(_path), 10, 3, () => Now)
            {
                UseWitnessAnchors = witnesses
            };

        private static void Run(Task t)
        {
            Assert.IsTrue(t.IsCompleted, "the editor fake should complete synchronously");
            t.GetAwaiter().GetResult();
        }

        private AnchoredCalibrationSession Calibrated(bool witnesses = true)
        {
            AnchoredCalibrationSession s = NewSession(witnesses);
            Run(s.StartAsync());
            Run(s.OnCapturedAsync(Low, High, Toward, Lowest, Highest, Depth));
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            return s;
        }

        private PianoProfile OnDisk() => new PianoProfileSerializer(_path).Load().Store.ActiveProfile;

        private static Vector3 RealMiddleC =>
            PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Toward, Depth).FrontEdgeCenter(AnchoredKeyboard.MiddleC);

        private static void Touch(AnchoredCalibrationSession s)
        {
            Run(s.ToggleCorrectionAsync());
            Run(s.OnMiddleCTouchedAsync(RealMiddleC));
        }

        [Test]
        public void OffUnlessAskedFor()
        {
            Calibrated(witnesses: false);
            Assert.AreEqual(1, _fake.CreateCount);
            Assert.AreEqual(0, OnDisk().witnessAnchors.Count);
        }

        [Test]
        public void ACalibrationMakesTwoSavedWitnessesAndOnePrimary()
        {
            Calibrated();
            Assert.AreEqual(3, _fake.CreateCount);
            Assert.AreEqual(3, _fake.SaveCount);
            Assert.AreEqual(1, OnDisk().anchorUuids.Count, "the keys stay on one anchor");
            Assert.AreEqual(2, OnDisk().witnessAnchors.Count);
            Assert.AreEqual("low", OnDisk().witnessAnchors[0].label);
            Assert.AreEqual("high", OnDisk().witnessAnchors[1].label);
        }

        [Test]
        public void ARelaunchLoadsThem()
        {
            Calibrated();
            _fake.EndSession();

            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());

            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.AreEqual(2, s.WitnessCount);
        }

        [Test]
        public void ARecalibrationErasesTheOldOnes()
        {
            AnchoredCalibrationSession s = Calibrated();
            Run(s.RecalibrateAsync());
            Run(s.OnCapturedAsync(Low, High, Toward, Lowest, Highest, Depth));

            Assert.AreEqual(3, _fake.Stored.Count, "one primary and two witnesses, no leftovers");
            foreach (WitnessAnchor w in OnDisk().witnessAnchors)
                Assert.IsTrue(_fake.Stored.ContainsKey(w.uuid));
        }

        [Test]
        public void AMiddleCTouchComparesEveryAnchorAgainstTheRealKey()
        {
            AnchoredCalibrationSession s = Calibrated();
            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.021f, 0f, 0f));

            Touch(s);

            float[] errors = s.LastWitnessErrorsMm;
            Assert.AreEqual(4, errors.Length, "primary, low, high, all together");
            Assert.AreEqual(21f, errors[0], 0.1f, "the primary drifted");
            Assert.AreEqual(0f, errors[1], 0.1f, "the low witness did not");
            Assert.AreEqual(0f, errors[2], 0.1f, "nor the high one");
            Assert.AreEqual(7f, errors[3], 0.1f, "the average of the three");
        }

        [Test]
        public void AnAcceptedFixBecomesTheNewBaseline()
        {
            AnchoredCalibrationSession s = Calibrated();
            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.021f, 0f, 0f));
            Touch(s);

            Touch(s);

            foreach (float e in s.LastWitnessErrorsMm) Assert.AreEqual(0f, e, 0.1f);

            // And that baseline survives a relaunch.
            _fake.EndSession();
            AnchoredCalibrationSession next = NewSession();
            Run(next.StartAsync());
            Touch(next);
            foreach (float e in next.LastWitnessErrorsMm) Assert.AreEqual(0f, e, 0.1f);
        }

        [Test]
        public void TheKeysStayOnThePrimary()
        {
            AnchoredCalibrationSession s = Calibrated();
            Vector3 before = s.BuildLayout().FrontEdgeCenter(AnchoredKeyboard.MiddleC);

            _fake.SimulateAnchorDrift(OnDisk().witnessAnchors[0].uuid, new Vector3(0.05f, 0f, 0f));

            Assert.Less((before - s.BuildLayout().FrontEdgeCenter(AnchoredKeyboard.MiddleC)).magnitude, 1e-5f);
        }

        [Test]
        public void ARecenterMovesEveryAnchorSoNothingLooksLikeDrift()
        {
            AnchoredCalibrationSession s = Calibrated();
            _fake.SimulateRecenter(new Pose(new Vector3(1f, 0f, -2f), Quaternion.Euler(0f, 90f, 0f)));

            Run(s.ToggleCorrectionAsync());
            Run(s.OnMiddleCTouchedAsync(s.BuildLayout().FrontEdgeCenter(AnchoredKeyboard.MiddleC)));

            foreach (float e in s.LastWitnessErrorsMm) Assert.AreEqual(0f, e, 0.1f);
        }
    }
}
