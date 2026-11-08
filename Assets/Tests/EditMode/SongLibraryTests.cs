using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class SongLibraryTests
    {
        private static byte[] Midi(int tracks, params byte[][] chunks)
        {
            var bytes = new List<byte>
            {
                (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6,
                0, 1, 0, (byte)tracks, 0x01, 0xE0,
            };
            foreach (byte[] chunk in chunks) bytes.AddRange(chunk);
            return bytes.ToArray();
        }

        private static byte[] Track(string name, int pitch, params byte[] extra)
        {
            var body = new List<byte>();
            if (name != null)
            {
                body.AddRange(new byte[] { 0x00, 0xFF, 0x03, (byte)name.Length });
                foreach (char c in name) body.Add((byte)c);
            }
            body.AddRange(extra);
            body.AddRange(new byte[] { 0x00, 0x90, (byte)pitch, 90, 0x83, 0x60, 0x80, (byte)pitch, 0, 0x00, 0xFF, 0x2F, 0x00 });

            var chunk = new List<byte> { (byte)'M', (byte)'T', (byte)'r', (byte)'k', 0, 0, (byte)(body.Count >> 8), (byte)body.Count };
            chunk.AddRange(body);
            return chunk.ToArray();
        }

        private static SongFile Named(string file) =>
            new SongFile(file, Midi(2, Track("Right", 72), Track("Left", 48)));

        private static SongFile Guessed(string file) =>
            new SongFile(file, Midi(2, Track("One", 72), Track("Two", 48)));

        private static SongFile Unknown(string file) => new SongFile(file, Midi(1, Track("Piano", 60)));

        private static SongFile Broken(string file) => new SongFile(file, new byte[] { 1, 2, 3 });

        private static SongLibrary Build(params SongFile[] own) =>
            SongLibrary.Build(new[] { Named("Twinkle.mid"), Named("Ode.mid") }, own);

        [Test]
        public void BuiltInsComeFirstAndOwnFilesAreSortedByName()
        {
            SongLibrary library = Build(Named("zebra.mid"), Named("Apple.mid"), Named("mango.mid"));

            Assert.AreEqual(2, library.BuiltInCount);
            Assert.AreEqual(3, library.OwnCount);
            string[] order = { "Twinkle.mid", "Ode.mid", "Apple.mid", "mango.mid", "zebra.mid" };
            for (int i = 0; i < order.Length; i++) Assert.AreEqual(order[i], library.Rows[i].FileName);
            Assert.IsTrue(library.Rows[1].IsBuiltIn);
            Assert.IsFalse(library.Rows[2].IsBuiltIn);
        }

        [Test]
        public void ABrokenFileIsAGreyedRowWithItsReason()
        {
            SongRow row = Build(Broken("bad_file.mid")).Rows[2];
            Assert.IsFalse(row.CanLoad);
            Assert.AreEqual("Not a MIDI file", row.Detail);
            Assert.AreEqual("bad file", row.Title);
            Assert.IsNull(row.Song);
        }

        [Test]
        public void AFileThatCouldNotBeOpenedKeepsThatReason()
        {
            SongRow row = Build(new SongFile("locked.mid", null, "Couldn't open this file")).Rows[2];
            Assert.IsFalse(row.CanLoad);
            Assert.AreEqual("Couldn't open this file", row.Detail);
        }

        [Test]
        public void NamedSongsShowNoHandLabel()
        {
            SongRow row = Build().Rows[0];
            Assert.AreEqual("4/4 · 120 BPM · 1 bar", row.Detail);
        }

        [Test]
        public void GuessedSongsSaySo()
        {
            StringAssert.EndsWith("Hands guessed", Build(Guessed("g.mid")).Rows[2].Detail);
        }

        [Test]
        public void UnknownSongsSaySoAndOfferOnlyAllNotes()
        {
            SongRow row = Build(Unknown("u.mid")).Rows[2];
            StringAssert.Contains("Hands unknown", row.Detail);
            StringAssert.EndsWith("All notes", row.Detail);
            Assert.IsFalse(row.OffersHands);
            Assert.AreEqual(PracticeHand.All, row.HandFor(PracticeHand.Left));
            Assert.AreEqual(PracticeHand.All, row.HandFor(PracticeHand.Right));
        }

        [Test]
        public void SongsWithHandsPlayTheOneChosen()
        {
            SongRow row = Build().Rows[0];
            Assert.IsTrue(row.OffersHands);
            Assert.AreEqual(PracticeHand.Left, row.HandFor(PracticeHand.Left));
            Assert.AreEqual(PracticeHand.Right, row.HandFor(PracticeHand.Right));
            Assert.AreEqual(PracticeHand.Right, row.HandFor(PracticeHand.All), "no All notes on a song with hands");
        }

        [Test]
        public void TheRememberedSongIsRestored()
        {
            SongLibrary library = Build(Named("mine.mid"));
            Assert.AreEqual("mine.mid", library.Restore(SongLibrary.KeyFor("mine.mid", false)).FileName);
        }

        [Test]
        public void AGoneOrBrokenSongFallsBackToTheFirstBuiltIn()
        {
            SongLibrary library = Build(Broken("mine.mid"));
            Assert.AreEqual("Twinkle.mid", library.Restore(SongLibrary.KeyFor("gone.mid", false)).FileName);
            Assert.AreEqual("Twinkle.mid", library.Restore(SongLibrary.KeyFor("mine.mid", false)).FileName);
            Assert.AreEqual("Twinkle.mid", library.Restore(null).FileName);
        }

        [Test]
        public void ABuiltInAndAnOwnFileWithOneNameAreDifferentRows()
        {
            SongLibrary library = Build(Unknown("Ode.mid"));
            Assert.IsTrue(library.Find(SongLibrary.KeyFor("Ode.mid", true)).OffersHands);
            Assert.IsFalse(library.Find(SongLibrary.KeyFor("Ode.mid", false)).OffersHands);
        }

        [Test]
        public void SwapHandsIsAskedPerFile()
        {
            SongLibrary library = SongLibrary.Build(
                new[] { Named("a.mid"), Named("b.mid") }, new SongFile[0], file => file == "b.mid");

            Assert.AreEqual(NoteHand.Left, library.Rows[0].Song.Notes[0].Hand, "the low note");
            Assert.AreEqual(NoteHand.Right, library.Rows[1].Song.Notes[0].Hand, "swapped");
        }

        [Test]
        public void RowsFallIntoPagesOfFive()
        {
            SongLibrary five = SongLibrary.Build(new[] { Named("1"), Named("2"), Named("3"), Named("4"), Named("5") }, new SongFile[0]);
            Assert.AreEqual(1, five.PageCount);

            SongLibrary six = SongLibrary.Build(new[] { Named("1"), Named("2"), Named("3"), Named("4"), Named("5"), Named("6") }, new SongFile[0]);
            Assert.AreEqual(2, six.PageCount);
            Assert.AreEqual(0, six.PageOf(six.Rows[4]));
            Assert.AreEqual(1, six.PageOf(six.Rows[5]));
        }

        [Test]
        public void AnEmptyLibraryStillHasOnePage()
        {
            SongLibrary none = SongLibrary.Build(new SongFile[0], new SongFile[0]);
            Assert.AreEqual(1, none.PageCount);
            Assert.IsNull(none.Restore(null));
        }
    }
}
