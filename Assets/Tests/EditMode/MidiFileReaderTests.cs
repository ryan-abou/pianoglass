using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// Tiny files typed in byte by byte from the MIDI rules, on purpose not
    /// made by StarterSongWriter, so a mistake in both can't hide.
    /// </summary>
    public class MidiFileReaderTests
    {
        private const byte MetaEvent = 0xFF;
        private static readonly byte[] EndOfTrack = { 0x00, MetaEvent, 0x2F, 0x00 };

        private static byte[] Header(int format, int tracks, int division) => new byte[]
        {
            (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6,
            0, (byte)format, 0, (byte)tracks, (byte)(division >> 8), (byte)division,
        };

        private static byte[] Track(params byte[] events)
        {
            var bytes = new List<byte> { (byte)'M', (byte)'T', (byte)'r', (byte)'k', 0, 0 };
            int length = events.Length + EndOfTrack.Length;
            bytes.Add((byte)(length >> 8));
            bytes.Add((byte)length);
            bytes.AddRange(events);
            bytes.AddRange(EndOfTrack);
            return bytes.ToArray();
        }

        private static byte[] File(params byte[][] chunks)
        {
            var bytes = new List<byte>();
            foreach (byte[] chunk in chunks) bytes.AddRange(chunk);
            return bytes.ToArray();
        }

        private static byte[] OneTrack(params byte[] events) => File(Header(0, 1, 480), Track(events));

        [Test]
        public void ReadsFormatZero()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(
                0x00, 0x90, 60, 100,
                0x83, 0x60, 0x80, 60, 0));   // delta 480 as two bytes

            Assert.AreEqual(0, f.Format);
            Assert.AreEqual(480, f.TicksPerBeat);
            Assert.AreEqual(1, f.Tracks.Count);
            Assert.AreEqual(1, f.Tracks[0].Notes.Count);
            MidiNote n = f.Tracks[0].Notes[0];
            Assert.AreEqual(60, n.Pitch);
            Assert.AreEqual(0, n.StartTick);
            Assert.AreEqual(480, n.EndTick);
        }

        [Test]
        public void ReadsFormatOneWithNamesTemposAndTimeSignatures()
        {
            byte[] conductor = Track(
                0x00, MetaEvent, 0x03, 0x04, (byte)'S', (byte)'o', (byte)'n', (byte)'g',
                0x00, MetaEvent, 0x58, 0x04, 3, 2, 24, 8,                  // 3/4
                0x00, MetaEvent, 0x51, 0x03, 0x07, 0xA1, 0x20);            // 500000 us
            byte[] notes = Track(
                0x00, MetaEvent, 0x03, 0x05, (byte)'R', (byte)'i', (byte)'g', (byte)'h', (byte)'t',
                0x00, 0x90, 72, 90,
                0x60, 0x80, 72, 0);

            MidiFile f = MidiFileReader.Read(File(Header(1, 2, 96), conductor, notes));

            Assert.AreEqual(1, f.Format);
            Assert.AreEqual(96, f.TicksPerBeat);
            Assert.AreEqual(2, f.Tracks.Count);
            Assert.AreEqual("Song", f.Tracks[0].Name);
            Assert.AreEqual(0, f.Tracks[0].Notes.Count);
            Assert.AreEqual("Right", f.Tracks[1].Name);
            Assert.AreEqual(1, f.Tempos.Count);
            Assert.AreEqual(500000, f.Tempos[0].MicrosPerQuarter);
            Assert.AreEqual(1, f.TimeSignatures.Count);
            Assert.AreEqual(3, f.TimeSignatures[0].Numerator);
            Assert.AreEqual(4, f.TimeSignatures[0].Denominator);
        }

        [Test]
        public void RunningStatusKeepsTheLastStatusByte()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(
                0x00, 0x90, 60, 100,
                0x10, 62, 100,          // still note-on
                0x10, 60, 0,            // still note-on, velocity 0
                0x10, 62, 0));

            List<MidiNote> notes = f.Tracks[0].Notes;
            Assert.AreEqual(2, notes.Count);
            Assert.AreEqual(62, notes[1].Pitch);
            Assert.AreEqual(16, notes[1].StartTick);
            Assert.AreEqual(32, notes[0].EndTick);
            Assert.AreEqual(48, notes[1].EndTick);
        }

        [Test]
        public void NoteOnWithVelocityZeroIsANoteOff()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(0x00, 0x90, 60, 100, 0x40, 0x90, 60, 0));
            Assert.AreEqual(1, f.Tracks[0].Notes.Count);
            Assert.AreEqual(64, f.Tracks[0].Notes[0].EndTick);
        }

        [TestCase(new byte[] { 0x7F }, 127)]
        [TestCase(new byte[] { 0x81, 0x00 }, 128)]
        [TestCase(new byte[] { 0xFF, 0x7F }, 16383)]
        [TestCase(new byte[] { 0x81, 0x80, 0x00 }, 16384)]
        [TestCase(new byte[] { 0xFF, 0xFF, 0x7F }, 2097151)]
        [TestCase(new byte[] { 0x81, 0x80, 0x80, 0x00 }, 2097152)]
        public void VariableLengthNumbersReadFromOneToFourBytes(byte[] delta, int expected)
        {
            var events = new List<byte>(delta) { 0x90, 60, 100 };
            MidiFile f = MidiFileReader.Read(OneTrack(events.ToArray()));
            Assert.AreEqual(expected, f.Tracks[0].Notes[0].StartTick);
        }

        [Test]
        public void TheSameKeyStruckTwicePairsFirstInFirstOut()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(
                0x00, 0x90, 60, 100,
                0x10, 0x90, 60, 100,
                0x10, 0x80, 60, 0,
                0x10, 0x80, 60, 0));

            List<MidiNote> notes = f.Tracks[0].Notes;
            Assert.AreEqual(2, notes.Count);
            Assert.AreEqual(0, notes[0].StartTick);
            Assert.AreEqual(32, notes[0].EndTick);
            Assert.AreEqual(16, notes[1].StartTick);
            Assert.AreEqual(48, notes[1].EndTick);
        }

        [Test]
        public void AZeroLengthNoteIsKept()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(0x00, 0x90, 60, 100, 0x00, 0x80, 60, 0));
            Assert.AreEqual(1, f.Tracks[0].Notes.Count);
            Assert.AreEqual(f.Tracks[0].Notes[0].StartTick, f.Tracks[0].Notes[0].EndTick);
        }

        [Test]
        public void AKeyNeverReleasedEndsWithTheTrack()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(
                0x00, 0x90, 60, 100,
                0x60, 0xB0, 64, 0));   // a pedal event, so the track runs on
            Assert.AreEqual(96, f.Tracks[0].Notes[0].EndTick);
        }

        [Test]
        public void TheDrumChannelIsIgnored()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(
                0x00, 0x99, 60, 100,
                0x10, 0x89, 60, 0,
                0x00, 0x90, 62, 100,
                0x10, 0x80, 62, 0));
            Assert.AreEqual(1, f.Tracks[0].Notes.Count);
            Assert.AreEqual(62, f.Tracks[0].Notes[0].Pitch);
        }

        [Test]
        public void NotesOffThePianoAreSkippedAndCounted()
        {
            MidiFile f = MidiFileReader.Read(OneTrack(
                0x00, 0x90, 20, 100,   // below A0
                0x00, 0x90, 21, 100,   // A0, kept
                0x00, 0x90, 108, 100,  // C8, kept
                0x00, 0x90, 109, 100,  // above C8
                0x10, 0x80, 20, 0,
                0x00, 0x80, 21, 0,
                0x00, 0x80, 108, 0,
                0x00, 0x80, 109, 0));

            Assert.AreEqual(2, f.Tracks[0].Notes.Count);
            Assert.AreEqual(2, f.NotesOutOfRange);
        }

        [Test]
        public void ProgramChangesAndPedalAreStepped()
        {
            // Two-byte and three-byte messages, a system message and a text
            // event, so a wrong length would throw the note after them off.
            MidiFile f = MidiFileReader.Read(OneTrack(
                0x00, 0xC0, 0,
                0x00, 0xB0, 64, 127,
                0x00, 0xF0, 0x02, 0x7E, 0xF7,
                0x00, MetaEvent, 0x01, 0x02, (byte)'h', (byte)'i',
                0x00, 0x90, 60, 100,
                0x10, 0x80, 60, 0));
            Assert.AreEqual(1, f.Tracks[0].Notes.Count);
            Assert.AreEqual(60, f.Tracks[0].Notes[0].Pitch);
        }

        [Test]
        public void AnUnknownChunkIsSkipped()
        {
            byte[] unknown = { (byte)'X', (byte)'F', (byte)'I', (byte)'H', 0, 0, 0, 2, 1, 2 };
            MidiFile f = MidiFileReader.Read(File(Header(0, 1, 480), unknown, Track(0x00, 0x90, 60, 100, 0x10, 0x80, 60, 0)));
            Assert.AreEqual(1, f.Tracks[0].Notes.Count);
        }

        [Test]
        public void JunkIsNotAMidiFile()
        {
            var e = Assert.Throws<MidiFormatException>(() =>
                MidiFileReader.Read(System.Text.Encoding.ASCII.GetBytes("this is just a text file, not music")));
            Assert.AreEqual("Not a MIDI file", e.Message);
        }

        [Test]
        public void AnEmptyFileIsNotAMidiFile()
        {
            Assert.Throws<MidiFormatException>(() => MidiFileReader.Read(new byte[0]));
            Assert.Throws<MidiFormatException>(() => MidiFileReader.Read(null));
        }

        [Test]
        public void EveryTruncationGivesACleanError()
        {
            byte[] whole = File(Header(1, 1, 480), Track(
                0x00, MetaEvent, 0x51, 0x03, 0x07, 0xA1, 0x20,
                0x00, 0x90, 60, 100,
                0x83, 0x60, 0x80, 60, 0));

            for (int length = 0; length < whole.Length; length++)
            {
                var cut = new byte[length];
                System.Array.Copy(whole, cut, length);
                Assert.Throws<MidiFormatException>(() => MidiFileReader.Read(cut), $"cut to {length} bytes");
            }
        }

        [Test]
        public void AnEventLongerThanItsChunkIsCaught()
        {
            byte[] bad = File(Header(0, 1, 480), new byte[]
            {
                (byte)'M', (byte)'T', (byte)'r', (byte)'k', 0, 0, 0, 2,
                0x00, 0x90, // the two data bytes are outside the chunk
                60, 100,
            });
            Assert.Throws<MidiFormatException>(() => MidiFileReader.Read(bad));
        }

        [Test]
        public void DataWithNoStatusIsBad()
        {
            var e = Assert.Throws<MidiFormatException>(() => MidiFileReader.Read(OneTrack(0x00, 60, 100)));
            StringAssert.Contains("isn't MIDI", e.Message);
        }

        [Test]
        public void ANumberLongerThanFourBytesIsBad()
        {
            Assert.Throws<MidiFormatException>(() =>
                MidiFileReader.Read(OneTrack(0x81, 0x81, 0x81, 0x81, 0x01, 0x90, 60, 100)));
        }

        [Test]
        public void FormatTwoAndClockTimingSayWhy()
        {
            StringAssert.Contains("format 2", Assert.Throws<MidiFormatException>(() =>
                MidiFileReader.Read(File(Header(2, 1, 480), Track()))).Message);
            StringAssert.Contains("clock", Assert.Throws<MidiFormatException>(() =>
                MidiFileReader.Read(File(Header(0, 1, 0xE728), Track()))).Message);
        }
    }
}
