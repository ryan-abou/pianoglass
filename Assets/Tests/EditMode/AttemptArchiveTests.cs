using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PianoGlass.Scoring;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// #4: earlier attempts are read once, off the main thread, and kept as
    /// headers; the results card compares with them from memory.
    /// </summary>
    public class AttemptArchiveTests
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "pg-archive-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static AttemptRecord Attempt(string started, double accuracy, int notes = 3)
        {
            var rec = new AttemptRecord
            {
                song = "Minuet", startedUtc = started, outcome = AttemptRecord.Completed,
                tempo = 1.0, accuracy = accuracy, rank = "A",
            };
            for (int i = 0; i < notes; i++) rec.notes.Add(new JudgementResult { noteIndex = i, pitch = 60 + i });
            rec.bars.Add(new BarStats { bar = 1, notes = notes });
            return rec;
        }

        private string Save(string name, AttemptRecord rec)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, JsonUtility.ToJson(rec, true));
            return path;
        }

        [Test]
        public void LoadKeepsHeadersWithoutTheNotes()
        {
            Save("a.json", Attempt("2026-11-21T10:00:00Z", 80));
            Save("b.json", Attempt("2026-11-21T10:05:00Z", 90));
            var archive = new AttemptArchive();
            archive.Load(_dir);

            List<AttemptRecord> all = archive.AllExcept(null);
            Assert.AreEqual(2, all.Count);
            foreach (AttemptRecord r in all)
            {
                Assert.AreEqual("Minuet", r.song);
                Assert.AreEqual(0, r.notes.Count);
                Assert.AreEqual(0, r.bars.Count);
            }
        }

        [Test]
        public void HeaderOnlyLeavesTheOriginalAlone()
        {
            AttemptRecord rec = Attempt("2026-11-21T10:00:00Z", 80);
            AttemptRecord header = rec.HeaderOnly();
            Assert.AreEqual(3, rec.notes.Count);
            Assert.AreEqual(0, header.notes.Count);
            Assert.AreEqual(rec.accuracy, header.accuracy);
            Assert.AreEqual(rec.startedUtc, header.startedUtc);
        }

        [Test]
        public void ANewSaveReplacesTheSameFileInsteadOfAddingIt()
        {
            string path = Save("a.json", Attempt("2026-11-21T10:00:00Z", 50));
            var archive = new AttemptArchive();
            archive.Load(_dir);
            archive.Put(path, Attempt("2026-11-21T10:00:00Z", 75));

            List<AttemptRecord> all = archive.AllExcept(null);
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(75, all[0].accuracy);
        }

        [Test]
        public void ALoadAfterASaveKeepsTheSave()
        {
            // The launch read can still be going when the first attempt is saved.
            string path = Save("a.json", Attempt("2026-11-21T10:00:00Z", 50));
            var archive = new AttemptArchive();
            archive.Put(path, Attempt("2026-11-21T10:00:00Z", 75));
            archive.Load(_dir);

            List<AttemptRecord> all = archive.AllExcept(null);
            Assert.AreEqual(1, all.Count);
            Assert.AreEqual(75, all[0].accuracy);
        }

        [Test]
        public void TheCurrentAttemptIsLeftOut()
        {
            Save("a.json", Attempt("2026-11-21T10:00:00Z", 80));
            string current = Path.Combine(_dir, "b.json");
            var archive = new AttemptArchive();
            archive.Load(_dir);
            archive.Put(current, Attempt("2026-11-21T10:05:00Z", 90));

            List<AttemptRecord> earlier = archive.AllExcept(current);
            Assert.AreEqual(1, earlier.Count);
            Assert.AreEqual(80, earlier[0].accuracy);
        }

        [Test]
        public void UnreadableFilesAreSkippedAndCounted()
        {
            Save("a.json", Attempt("2026-11-21T10:00:00Z", 80));
            File.WriteAllText(Path.Combine(_dir, "broken.json"), "not json");
            var archive = new AttemptArchive();
            archive.Load(_dir);
            Assert.AreEqual(1, archive.Count);
            Assert.AreEqual(1, archive.Unreadable);
        }

        [Test]
        public void AMissingFolderIsEmpty()
        {
            var archive = new AttemptArchive();
            archive.Load(Path.Combine(_dir, "nothing here"));
            Assert.AreEqual(0, archive.AllExcept(null).Count);
            Assert.IsNull(archive.LoadError);
        }

        [Test]
        public void TheBackgroundReadIsWaitedFor()
        {
            for (int i = 0; i < 20; i++) Save($"{i:00}.json", Attempt($"2026-11-21T10:{i:00}:00Z", 60 + i));
            var archive = new AttemptArchive();
            bool doneCalled = false;
            archive.StartLoading(_dir, a => doneCalled = true);

            Assert.AreEqual(20, archive.AllExcept(null).Count);
            Assert.IsTrue(archive.IsLoaded);
            Assert.IsNull(archive.LoadError);
            // done runs right after the read, on the worker thread.
            System.Threading.SpinWait.SpinUntil(() => doneCalled, 1000);
            Assert.IsTrue(doneCalled);
        }

        [Test]
        public void TheCardComparesTheSameWayFromHeaders()
        {
            var full = new List<AttemptRecord>
            {
                Attempt("2026-11-21T10:00:00Z", 70),
                Attempt("2026-11-21T10:05:00Z", 80),
            };
            var headers = new List<AttemptRecord>();
            foreach (AttemptRecord r in full) headers.Add(r.HeaderOnly());
            AttemptRecord now = Attempt("2026-11-21T10:10:00Z", 85);

            ResultsSummary fromFull = ResultsSummary.From(now, full);
            ResultsSummary fromHeaders = ResultsSummary.From(now, headers);
            Assert.AreEqual(fromFull.AttemptNumber, fromHeaders.AttemptNumber);
            Assert.AreEqual(3, fromHeaders.AttemptNumber);
            Assert.AreEqual(fromFull.IsNewBest, fromHeaders.IsNewBest);
            Assert.AreEqual(fromFull.Change, fromHeaders.Change);
        }
    }
}
