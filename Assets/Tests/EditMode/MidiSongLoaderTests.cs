using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class MidiSongLoaderTests
    {
        private const double Eps = 1e-9;

        private static byte[] File(int tracks, params byte[][] chunks)
        {
            var bytes = new List<byte>
            {
                (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6,
                0, 1, 0, (byte)tracks, 0x01, 0xE0, // format 1, 480 ticks per beat
            };
            foreach (byte[] chunk in chunks) bytes.AddRange(chunk);
            return bytes.ToArray();
        }

        private static byte[] Track(string name, params byte[] events)
        {
            var body = new List<byte>();
            if (name != null)
            {
                body.AddRange(new byte[] { 0x00, 0xFF, 0x03, (byte)name.Length });
                foreach (char c in name) body.Add((byte)c);
            }
            body.AddRange(events);
            body.AddRange(new byte[] { 0x00, 0xFF, 0x2F, 0x00 });

            var chunk = new List<byte> { (byte)'M', (byte)'T', (byte)'r', (byte)'k', 0, 0, (byte)(body.Count >> 8), (byte)body.Count };
            chunk.AddRange(body);
            return chunk.ToArray();
        }

        private static readonly byte[] QuarterC4 = { 0x00, 0x90, 60, 90, 0x83, 0x60, 0x80, 60, 0 };
        private static readonly byte[] QuarterC6 = { 0x00, 0x90, 84, 90, 0x83, 0x60, 0x80, 84, 0 };

        [Test]
        public void HandsComeFromTrackNamesAndNotesBecomeSeconds()
        {
            SongLoadReport r = MidiSongLoader.Load(File(2, Track("Right Hand", QuarterC6), Track("Left Hand", QuarterC4)), "a.mid");

            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(HandsStatus.Named, r.Song.Hands);
            Assert.AreEqual(2, r.Song.Notes.Count);
            Assert.AreEqual(60, r.Song.Notes[0].Pitch, "sorted by time, then pitch");
            Assert.AreEqual(NoteHand.Left, r.Song.Notes[0].Hand);
            Assert.AreEqual(84, r.Song.Notes[1].Pitch);
            Assert.AreEqual(NoteHand.Right, r.Song.Notes[1].Hand);
            Assert.AreEqual(0.5, r.Song.Notes[0].Duration, Eps);
        }

        [Test]
        public void EmptyTracksDontCount()
        {
            byte[] conductor = Track("My Tune", 0x00, 0xFF, 0x51, 0x03, 0x07, 0xA1, 0x20);
            SongLoadReport r = MidiSongLoader.Load(File(2, conductor, Track("Piano", QuarterC4)), "a.mid");
            Assert.AreEqual(HandsStatus.Unknown, r.Song.Hands, "only one track has notes");
            Assert.AreEqual(NoteHand.None, r.Song.Notes[0].Hand);
        }

        [Test]
        public void TwoUnnamedTracksAreGuessed()
        {
            SongLoadReport r = MidiSongLoader.Load(File(2, Track(null, QuarterC4), Track(null, QuarterC6)), "a.mid");
            Assert.AreEqual(HandsStatus.Guessed, r.Song.Hands);
            Assert.AreEqual(NoteHand.Left, r.Song.Notes[0].Hand);
            Assert.AreEqual(NoteHand.Right, r.Song.Notes[1].Hand);
        }

        [Test]
        public void SwapHandsFlipsThem()
        {
            SongLoadReport r = MidiSongLoader.Load(File(2, Track("Right", QuarterC6), Track("Left", QuarterC4)), "a.mid", swapHands: true);
            Assert.AreEqual(NoteHand.Right, r.Song.Notes[0].Hand);
            Assert.AreEqual(NoteHand.Left, r.Song.Notes[1].Hand);
        }

        [Test]
        public void AZeroLengthNoteGetsTheMinimumLength()
        {
            SongLoadReport r = MidiSongLoader.Load(File(1, Track("P", 0x00, 0x90, 60, 90, 0x00, 0x80, 60, 0)), "a.mid");
            Assert.AreEqual(1, r.Song.Notes.Count);
            Assert.AreEqual(MidiSongLoader.MinNoteSeconds, r.Song.Notes[0].Duration, Eps);
        }

        [Test]
        public void NotesOffThePianoAreCountedInTheReport()
        {
            byte[] events = { 0x00, 0x90, 10, 90, 0x10, 0x80, 10, 0, 0x00, 0x90, 60, 90, 0x10, 0x80, 60, 0 };
            SongLoadReport r = MidiSongLoader.Load(File(1, Track("P", events)), "a.mid");
            Assert.AreEqual(1, r.NotesOutOfRange);
            Assert.AreEqual(1, r.Song.Notes.Count);
        }

        [Test]
        public void TheReportCountsMovedAndMergedNotes()
        {
            // At 120 BPM with 480 ticks a beat, one tick is about 1 ms.
            byte[] right = { 0x00, 0x90, 60, 90, 0x83, 0x60, 0x80, 60, 0 };
            byte[] left = { 0x14, 0x90, 64, 90, 0x83, 0x60, 0x80, 64, 0, 0x00, 0x90, 60, 90, 0x10, 0x80, 60, 0 };
            SongLoadReport r = MidiSongLoader.Load(File(2, Track("Right", right), Track("Left", left)), "a.mid");
            Assert.AreEqual(1, r.NotesMoved, "the left-hand E, 20 ticks after the chord started");
        }

        [Test]
        public void ABadFileGivesAReasonNotACrash()
        {
            SongLoadReport r = MidiSongLoader.Load(new byte[] { 1, 2, 3 }, "broken.mid");
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("Not a MIDI file", r.Error);
        }

        [Test]
        public void AFileWithNoNotesSaysSo()
        {
            SongLoadReport r = MidiSongLoader.Load(File(1, Track("Empty")), "a.mid");
            Assert.IsFalse(r.Ok);
            Assert.AreEqual("Has no notes to play", r.Error);
        }

        [Test]
        public void TheTitleComesFromANoteFreeFirstTrack()
        {
            byte[] conductor = Track("Minuet in G");
            SongLoadReport r = MidiSongLoader.Load(File(3, conductor, Track("Right", QuarterC6), Track("Left", QuarterC4)), "x.mid");
            Assert.AreEqual("Minuet in G", r.Song.Title);
        }

        [Test]
        public void ANoteTracksNameIsNotATitle()
        {
            SongLoadReport r = MidiSongLoader.Load(File(2, Track("Right", QuarterC6), Track("Left", QuarterC4)), "MySong.mid");
            Assert.AreEqual("My Song", r.Song.Title);
        }

        [TestCase("MinuetInG.mid", "Minuet in G")]
        [TestCase("OdeToJoy.mid", "Ode to Joy")]
        [TestCase("my_song-2.midi", "my song 2")]
        [TestCase("My Song.mid", "My Song")]
        [TestCase(".mid", "Untitled")]
        public void TitlesFromFileNames(string file, string title)
        {
            Assert.AreEqual(title, MidiSongLoader.TitleFromFileName(file));
        }
    }
}
