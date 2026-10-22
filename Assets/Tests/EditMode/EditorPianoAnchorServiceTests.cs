using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// The fake anchor service, and the create → save → relaunch → recenter
    /// → load round trip the real one will be driven through on device
    /// (P-01..P-03). Passing here says the maths and the contract agree;
    /// it says nothing about the Quest runtime.
    /// </summary>
    public class EditorPianoAnchorServiceTests
    {
        private static readonly Vector3 Low = new Vector3(-0.60f, 0.75f, 0.40f);
        private static readonly Vector3 High = new Vector3(0.599f, 0.75f, 0.40f);
        private static readonly Vector3 Toward = Vector3.back;

        [Test]
        public void SavedAnchor_SurvivesRelaunchAndRecenter_LayoutStaysOnThePhysicalKeys()
        {
            var svc = new EditorPianoAnchorService();
            Pose frame = AnchoredKeyboard.Frame(Low, High, Toward);

            AnchorResult created = svc.CreateAsync(frame.position, frame.rotation).Result;
            Assert.That(created.Ok, created.ToString());
            Assert.That(svc.SaveAsync(created.Handle).Result.Ok);

            KeyboardGeometry local = AnchoredKeyboard.ToLocal(created.Handle.Pose, Low, High, Toward);
            PianoKeyLayout before = AnchoredKeyboard.BuildLayout(created.Handle.Pose, local, 21, 108, 0.145f, Vector3.zero);

            svc.EndSession();
            Assert.That(created.Handle.IsAlive, Is.False);

            var recenter = new Pose(new Vector3(1.2f, 0f, -0.4f), Quaternion.Euler(0f, 37f, 0f));
            svc.SimulateRecenter(recenter);

            AnchorResult loaded = svc.LoadAsync(created.Uuid, 10).Result;
            Assert.That(loaded.Ok, loaded.ToString());
            PianoKeyLayout after = AnchoredKeyboard.BuildLayout(loaded.Handle.Pose, local, 21, 108, 0.145f, Vector3.zero);

            foreach (int note in new[] { 21, 60, 108 })
            {
                Vector3 expected = recenter.position + recenter.rotation * before.FrontEdgeCenter(note);
                AnchoredKeyboardTests.AssertApprox(expected, after.FrontEdgeCenter(note), 1e-4f);
            }
            Assert.That(svc.CreateCount, Is.EqualTo(1), "load must never create an anchor");
        }

        [Test]
        public void UnsavedAnchor_IsNotFoundAfterRelaunch()
        {
            var svc = new EditorPianoAnchorService();
            AnchorResult created = svc.CreateAsync(Vector3.zero, Quaternion.identity).Result;
            svc.EndSession();

            Assert.That(svc.LoadAsync(created.Uuid, 10).Result.Status, Is.EqualTo(AnchorStatus.NotFound));
        }

        [Test]
        public void UnknownUuid_IsNotFound_NotFailed()
        {
            var svc = new EditorPianoAnchorService();
            Assert.That(svc.LoadAsync(System.Guid.NewGuid().ToString("D"), 10).Result.Status,
                        Is.EqualTo(AnchorStatus.NotFound));
        }

        [Test]
        public void MalformedUuid_IsInvalidArgument()
        {
            var svc = new EditorPianoAnchorService();
            Assert.That(svc.LoadAsync("", 10).Result.Status, Is.EqualTo(AnchorStatus.InvalidArgument));
            Assert.That(svc.LoadAsync("not-a-guid", 10).Result.Status, Is.EqualTo(AnchorStatus.InvalidArgument));
            Assert.That(svc.EraseByUuidAsync("nope").Result.Status, Is.EqualTo(AnchorStatus.InvalidArgument));
        }

        [Test]
        public void ForcedTimeout_LeavesTheStoredAnchorAlone()
        {
            var svc = new EditorPianoAnchorService();
            AnchorResult created = svc.CreateAsync(Vector3.zero, Quaternion.identity).Result;
            svc.SaveAsync(created.Handle).Wait();
            svc.EndSession();

            svc.ForceLoad = AnchorStatus.Timeout;
            Assert.That(svc.LoadAsync(created.Uuid, 1).Result.Status, Is.EqualTo(AnchorStatus.Timeout));
            Assert.That(svc.Stored.ContainsKey(created.Uuid), "a failed localize must not erase");

            svc.ForceLoad = null;
            Assert.That(svc.LoadAsync(created.Uuid, 1).Result.Ok);
        }

        [Test]
        public void Erase_RemovesStoredAnchorAndKillsHandle()
        {
            var svc = new EditorPianoAnchorService();
            AnchorResult created = svc.CreateAsync(Vector3.zero, Quaternion.identity).Result;
            svc.SaveAsync(created.Handle).Wait();

            Assert.That(svc.EraseAsync(created.Handle).Result.Ok);
            Assert.That(created.Handle.IsAlive, Is.False);
            Assert.That(svc.Stored.ContainsKey(created.Uuid), Is.False);
            Assert.That(svc.LoadAsync(created.Uuid, 1).Result.Status, Is.EqualTo(AnchorStatus.NotFound));
        }

        [Test]
        public void LoadTwiceInOneSession_ReturnsTheSameHandle()
        {
            var svc = new EditorPianoAnchorService();
            AnchorResult created = svc.CreateAsync(Vector3.one, Quaternion.identity).Result;
            svc.SaveAsync(created.Handle).Wait();

            AnchorResult again = svc.LoadAsync(created.Uuid, 1).Result;
            Assert.That(again.Ok);
            Assert.That(again.Handle, Is.SameAs(created.Handle));
        }

        [Test]
        public void ForcedSaveFailure_DoesNotPersist()
        {
            var svc = new EditorPianoAnchorService { ForceSave = AnchorStatus.Failed };
            AnchorResult created = svc.CreateAsync(Vector3.zero, Quaternion.identity).Result;

            Assert.That(svc.SaveAsync(created.Handle).Result.Status, Is.EqualTo(AnchorStatus.Failed));
            Assert.That(svc.Stored, Is.Empty);
        }
    }
}
