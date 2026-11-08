using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class NoteWindowTests
    {
        private static List<int> Showing(NoteWindow window, IReadOnlyList<SongNote> notes, double time, double lead)
        {
            window.Range(time, lead, out int from, out int to);
            var showing = new List<int>();
            for (int i = from; i < to; i++)
            {
                SongNote n = notes[window.NoteAt(i)];
                if (n.StartTime <= time + lead && n.EndTime >= time) showing.Add(window.NoteAt(i));
            }
            showing.Sort();
            return showing;
        }

        [Test]
        public void ItFindsExactlyWhatCheckingEveryNoteWouldFind()
        {
            var notes = new List<SongNote>();
            var random = new System.Random(7);
            for (int i = 0; i < 300; i++)
                notes.Add(new SongNote(60, random.NextDouble() * 60.0, random.NextDouble() * random.NextDouble() * 12.0));

            var window = new NoteWindow(notes);

            for (double time = -3.0; time < 75.0; time += 0.73)
            {
                var expected = new List<int>();
                for (int i = 0; i < notes.Count; i++)
                    if (notes[i].StartTime <= time + 3.0 && notes[i].EndTime >= time) expected.Add(i);
                CollectionAssert.AreEqual(expected, Showing(window, notes, time, 3.0), $"at {time}");
            }
        }

        [Test]
        public void ALongNoteKeepsItsPlaceAfterShorterOnesPass()
        {
            var notes = new[]
            {
                new SongNote(60, 0.0, 30.0),
                new SongNote(62, 1.0, 0.5),
                new SongNote(64, 2.0, 0.5),
            };
            var window = new NoteWindow(notes);
            CollectionAssert.Contains(Showing(window, notes, 20.0, 3.0), 0);
            CollectionAssert.DoesNotContain(Showing(window, notes, 20.0, 3.0), 1);
        }

        [Test]
        public void ItCopesWithNotesOutOfOrder()
        {
            var notes = new[] { new SongNote(60, 5.0, 1.0), new SongNote(62, 0.0, 1.0) };
            var window = new NoteWindow(notes);
            Assert.AreEqual(1, window.NoteAt(0));
            CollectionAssert.AreEqual(new[] { 0 }, Showing(window, notes, 4.0, 3.0));
        }

        [Test]
        public void NoNotesIsAnEmptyRange()
        {
            new NoteWindow(new SongNote[0]).Range(1.0, 3.0, out int from, out int to);
            Assert.AreEqual(0, from);
            Assert.AreEqual(0, to);
        }
    }
}
