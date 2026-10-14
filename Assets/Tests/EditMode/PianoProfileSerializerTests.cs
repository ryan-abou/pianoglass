using System;
using System.IO;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Schema v2: round trip, the never-overwrite-the-future rule,
    /// atomic writes, and one case per reject rule in report section 9.</summary>
    public class PianoProfileSerializerTests
    {
        private string _dir;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PianoGlassTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, PianoProfileSerializer.DefaultFileName);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, recursive: true); } catch (Exception) { }
        }

        private static PianoProfile ValidProfile()
        {
            float span = (PianoKeyLayout.CountWhiteKeys(21, 108) - 1) * PianoKeyLayout.StandardWhiteKeySpacing;
            return new PianoProfile
            {
                profileId = "3c1e0e4b-0000-4000-8000-000000000001",
                displayName = "Piano 1",
                anchorUuids = { "a7f00000-1111-4222-8333-444455556666" },
                lowestNote = 21,
                highestNote = 108,
                whiteKeyDepth = 0.145f,
                lowFrontLocal = new Vector3(-span / 2f, 0f, 0f),
                highFrontLocal = new Vector3(span / 2f, 0f, 0f),
                towardPlayerLocal = new Vector3(0f, 0f, -1f),
                createdUtc = "2026-10-13T20:00:00Z",
            };
        }

        private static PianoProfileStore ValidStore()
        {
            var p = ValidProfile();
            var s = new PianoProfileStore { activeProfileId = p.profileId };
            s.profiles.Add(p);
            return s;
        }

        // ------------------------------------------------------------------

        [Test]
        public void MissingFileIsFirstTimeSetup()
        {
            Assert.AreEqual(ProfileLoadStatus.Missing, new PianoProfileSerializer(_path).Load().Status);
        }

        [Test]
        public void RoundTripsV2()
        {
            var ser = new PianoProfileSerializer(_path);
            PianoProfileStore store = ValidStore();
            store.hasTipOffset = true;
            store.tipOffsetController = new Vector3(0f, -0.021f, 0.034f);
            store.tipOffsetRmsMm = 1.3f;
            store.tipNudgeKeyboardMm = new Vector3(1f, 0f, -2f);

            Assert.IsTrue(ser.Save(store, out string err), err);
            ProfileLoadResult r = ser.Load();

            Assert.AreEqual(ProfileLoadStatus.Loaded, r.Status, r.Reason);
            Assert.AreEqual(2, r.Store.schemaVersion);
            Assert.AreEqual(store.activeProfileId, r.Store.activeProfileId);
            Assert.IsTrue(r.Store.hasTipOffset);
            AnchoredKeyboardTests.AssertApprox(store.tipOffsetController, r.Store.tipOffsetController, 1e-6f);
            AnchoredKeyboardTests.AssertApprox(store.tipNudgeKeyboardMm, r.Store.tipNudgeKeyboardMm, 1e-6f);

            PianoProfile p = r.Store.ActiveProfile;
            Assert.IsNotNull(p);
            Assert.AreEqual("a7f00000-1111-4222-8333-444455556666", p.PrimaryAnchorUuid);
            AnchoredKeyboardTests.AssertApprox(ValidProfile().lowFrontLocal, p.lowFrontLocal, 1e-6f);
            Assert.IsNull(PianoProfileSerializer.ValidateProfile(p));
        }

        [Test]
        public void ARootWithNoProfilesIsValid()
        {
            var ser = new PianoProfileSerializer(_path);
            Assert.IsTrue(ser.Save(new PianoProfileStore(), out string err), err);
            ProfileLoadResult r = ser.Load();
            Assert.AreEqual(ProfileLoadStatus.Loaded, r.Status, r.Reason);
            Assert.IsNull(r.Store.ActiveProfile);
        }

        [Test]
        public void UnparseableFileIsRenamedAsideNotDeleted()
        {
            const string garbage = "{ this is not json";
            File.WriteAllText(_path, garbage);
            var ser = new PianoProfileSerializer(_path) { UtcNow = () => new DateTime(2026, 10, 13, 20, 0, 0, DateTimeKind.Utc) };

            ProfileLoadResult r = ser.Load();

            Assert.AreEqual(ProfileLoadStatus.Invalid, r.Status);
            Assert.IsFalse(File.Exists(_path));
            Assert.IsNotNull(r.RenamedTo);
            StringAssert.EndsWith("piano_profiles.invalid-20261013T200000Z.json", r.RenamedTo);
            Assert.AreEqual(garbage, File.ReadAllText(r.RenamedTo));
        }

        [Test]
        public void MissingSchemaVersionIsInvalid()
        {
            File.WriteAllText(_path, "{ \"profiles\": [] }");
            Assert.AreEqual(ProfileLoadStatus.Invalid, new PianoProfileSerializer(_path).Load().Status);
        }

        [Test]
        public void MissingProfilesIsInvalid()
        {
            File.WriteAllText(_path, "{ \"schemaVersion\": 2 }");
            Assert.AreEqual(ProfileLoadStatus.Invalid, new PianoProfileSerializer(_path).Load().Status);
        }

        [Test]
        public void FutureVersionIsReadButNeverOverwritten()
        {
            const string future = "{ \"schemaVersion\": 3, \"activeProfileId\": \"\", \"profiles\": [], \"somethingNew\": 1 }";
            File.WriteAllText(_path, future);
            var ser = new PianoProfileSerializer(_path);

            ProfileLoadResult r = ser.Load();
            Assert.AreEqual(ProfileLoadStatus.FutureVersion, r.Status);
            Assert.IsNotNull(r.Store);

            Assert.IsFalse(ser.Save(ValidStore(), out string err));
            StringAssert.Contains("newer", err);
            Assert.AreEqual(future, File.ReadAllText(_path), "byte-identical");
        }

        [Test]
        public void AFailedTempWriteLeavesTheOldFileIntact()
        {
            var ser = new PianoProfileSerializer(_path);
            Assert.IsTrue(ser.Save(ValidStore(), out string err), err);
            string before = File.ReadAllText(_path);

            ser.WriteAllText = (path, text) =>
            {
                File.WriteAllText(path, text.Substring(0, text.Length / 2));
                throw new IOException("disk full (simulated)");
            };

            PianoProfileStore changed = ValidStore();
            changed.activeProfileId = "something-else";
            Assert.IsFalse(ser.Save(changed, out err));

            Assert.AreEqual(before, File.ReadAllText(_path));
            Assert.IsFalse(File.Exists(_path + ".tmp"), "the half-written temp file is cleaned up");
        }

        // ------------------------------------------------------------------
        // Profile reject rules, one case each
        // ------------------------------------------------------------------

        [Test]
        public void AValidProfilePasses() => Assert.IsNull(PianoProfileSerializer.ValidateProfile(ValidProfile()));

        [Test]
        public void RejectsNonFiniteNumbers()
        {
            var p = ValidProfile();
            p.lowFrontLocal = new Vector3(float.NaN, 0f, 0f);
            Assert.IsNotNull(PianoProfileSerializer.ValidateProfile(p));
        }

        [TestCase(0.25f)]
        [TestCase(1.7f)]
        public void RejectsSpanOutsideThePlausibleRange(float span)
        {
            var p = ValidProfile();
            p.lowFrontLocal = new Vector3(-span / 2f, 0f, 0f);
            p.highFrontLocal = new Vector3(span / 2f, 0f, 0f);
            Assert.IsNotNull(PianoProfileSerializer.ValidateProfile(p));
        }

        [Test]
        public void RejectsWhiteKeySpacingOutsideNineteenToTwentyEightMillimetres()
        {
            var p = ValidProfile();
            p.lowestNote = 48; p.highestNote = 72; // 2 octaves over a 1.2 m span
            Assert.IsNotNull(PianoProfileSerializer.ValidateProfile(p));
        }

        [TestCase(20, 108)]
        [TestCase(21, 109)]
        [TestCase(60, 60)]
        [TestCase(72, 60)]
        public void RejectsBadNoteRanges(int low, int high)
        {
            var p = ValidProfile();
            p.lowestNote = low; p.highestNote = high;
            Assert.IsNotNull(PianoProfileSerializer.ValidateProfile(p));
        }

        [Test]
        public void RejectsTowardPlayerThatIsNotUnitLength()
        {
            var p = ValidProfile();
            p.towardPlayerLocal = new Vector3(0f, 0f, -0.5f);
            Assert.IsNotNull(PianoProfileSerializer.ValidateProfile(p));
        }

        [Test]
        public void RejectsTowardPlayerMoreThanFiveDegreesOffHorizontal()
        {
            var p = ValidProfile();
            p.towardPlayerLocal = Quaternion.AngleAxis(8f, Vector3.right) * new Vector3(0f, 0f, -1f);
            Assert.IsNotNull(PianoProfileSerializer.ValidateProfile(p));
        }

        [TestCase("")]
        [TestCase("not-a-uuid")]
        [TestCase("00000000-0000-0000-0000-000000000000")]
        public void RejectsAnUnusableAnchorUuid(string uuid)
        {
            var p = ValidProfile();
            p.anchorUuids.Clear();
            if (uuid.Length > 0) p.anchorUuids.Add(uuid);
            Assert.IsNotNull(PianoProfileSerializer.ValidateProfile(p));
        }
    }
}
