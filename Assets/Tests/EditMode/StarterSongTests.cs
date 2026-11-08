using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// The real files in Assets/Songs, read by the same loader the app uses.
    /// The expected notes are typed here by hand, so the writer's lists can't
    /// check themselves.
    /// </summary>
    public class StarterSongTests
    {
        private const double Eps = 1e-6;
        private const double Beat = 0.5; // every starter song is 120 BPM
        private const int G2 = 43, C3 = 48, F3 = 53, G3 = 55;
        private const int C4 = 60, D4 = 62, E4 = 64, F4 = 65, G4 = 67, A4 = 69;

        private static SongLoadReport Load(string file)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Application.dataPath, "Songs", file));
            return MidiSongLoader.Load(bytes, file);
        }

        private static List<SongNote> Hand(SongData song, NoteHand hand)
        {
            var notes = new List<SongNote>();
            foreach (SongNote n in song.Notes) if (n.Hand == hand) notes.Add(n);
            return notes;
        }

        /// <summary>(pitch, beats long), one after another from the first beat.</summary>
        private static void AssertRun(List<SongNote> actual, NoteHand hand, params (int pitch, double beats)[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count, $"{hand} hand note count");
            double beat = 0.0;
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].pitch, actual[i].Pitch, $"{hand} note {i} pitch");
                Assert.AreEqual(beat * Beat, actual[i].StartTime, Eps, $"{hand} note {i} start");
                Assert.AreEqual(expected[i].beats * Beat, actual[i].Duration, Eps, $"{hand} note {i} length");
                Assert.AreEqual(hand, actual[i].Hand, $"note {i} hand");
                beat += expected[i].beats;
            }
        }

        private static int RightHandNotesInBars(SongData song, int firstBar, int lastBar)
        {
            int count = 0;
            foreach (SongNote n in Hand(song, NoteHand.Right))
            {
                int bar = song.BarOf(n.StartTime);
                if (bar >= firstBar && bar <= lastBar) count++;
            }
            return count;
        }

        [Test]
        public void TwinkleIsEveryNoteItShouldBe()
        {
            SongLoadReport r = Load("TwinkleTwinkle.mid");
            Assert.IsTrue(r.Ok, r.Error);
            SongData song = r.Song;

            Assert.AreEqual("Twinkle, Twinkle, Little Star", song.Title);
            Assert.AreEqual(HandsStatus.Named, song.Hands);
            Assert.AreEqual(120f, song.Bpm, 0.01f);
            Assert.AreEqual(4, song.BeatsPerBar);
            Assert.AreEqual(8, song.BarCount);

            AssertRun(Hand(song, NoteHand.Right), NoteHand.Right,
                (C4, 1), (C4, 1), (G4, 1), (G4, 1), (A4, 1), (A4, 1), (G4, 2),
                (F4, 1), (F4, 1), (E4, 1), (E4, 1), (D4, 1), (D4, 1), (C4, 2),
                (G4, 1), (G4, 1), (F4, 1), (F4, 1), (E4, 1), (E4, 1), (D4, 2),
                (G4, 1), (G4, 1), (F4, 1), (F4, 1), (E4, 1), (E4, 1), (D4, 2));

            AssertRun(Hand(song, NoteHand.Left), NoteHand.Left,
                (C3, 4),
                (F3, 2), (C3, 2),
                (F3, 2), (C3, 2),
                (G3, 2), (C3, 2),
                (C3, 2), (F3, 2),
                (C3, 2), (G3, 2),
                (C3, 2), (F3, 2),
                (C3, 2), (G3, 2));
        }

        [Test]
        public void TwinklesRightHandIsTheOldHardCodedMelody()
        {
            List<SongNote> right = Hand(Load("TwinkleTwinkle.mid").Song, NoteHand.Right);
            IReadOnlyList<SongNote> old = SongData.TwinkleTwinkleLittleStar().Notes;

            Assert.AreEqual(old.Count, right.Count);
            for (int i = 0; i < old.Count; i++)
            {
                Assert.AreEqual(old[i].Pitch, right[i].Pitch, $"note {i}");
                Assert.AreEqual(old[i].StartTime, right[i].StartTime, Eps, $"note {i}");
                Assert.AreEqual(old[i].Duration, right[i].Duration, Eps, $"note {i}");
            }
        }

        [Test]
        public void OdeIsEveryNoteItShouldBe()
        {
            SongLoadReport r = Load("OdeToJoy.mid");
            Assert.IsTrue(r.Ok, r.Error);
            SongData song = r.Song;

            Assert.AreEqual("Ode to Joy", song.Title);
            Assert.AreEqual(HandsStatus.Named, song.Hands);
            Assert.AreEqual(120f, song.Bpm, 0.01f);
            Assert.AreEqual(4, song.BeatsPerBar);
            Assert.AreEqual(16, song.BarCount);

            AssertRun(Hand(song, NoteHand.Right), NoteHand.Right,
                (E4, 1), (E4, 1), (F4, 1), (G4, 1),   // 1
                (G4, 1), (F4, 1), (E4, 1), (D4, 1),   // 2
                (C4, 1), (C4, 1), (D4, 1), (E4, 1),   // 3
                (E4, 1.5), (D4, 0.5), (D4, 2),        // 4
                (E4, 1), (E4, 1), (F4, 1), (G4, 1),   // 5
                (G4, 1), (F4, 1), (E4, 1), (D4, 1),   // 6
                (C4, 1), (C4, 1), (D4, 1), (E4, 1),   // 7
                (D4, 1.5), (C4, 0.5), (C4, 2),        // 8
                (D4, 1), (D4, 1), (E4, 1), (C4, 1),   // 9
                (D4, 1), (E4, 0.5), (F4, 0.5), (E4, 1), (C4, 1), // 10
                (D4, 1), (E4, 0.5), (F4, 0.5), (E4, 1), (D4, 1), // 11
                (C4, 1), (D4, 1), (G3, 2),            // 12
                (E4, 1), (E4, 1), (F4, 1), (G4, 1),   // 13
                (G4, 1), (F4, 1), (E4, 1), (D4, 1),   // 14
                (C4, 1), (C4, 1), (D4, 1), (E4, 1),   // 15
                (D4, 1.5), (C4, 0.5), (C4, 2));       // 16

            AssertRun(Hand(song, NoteHand.Left), NoteHand.Left,
                (C3, 4), (G2, 4), (C3, 4), (C3, 2), (G2, 2),   // 1-4
                (C3, 4), (G2, 4), (C3, 4), (G2, 2), (C3, 2),   // 5-8
                (G2, 2), (C3, 2),                              // 9
                (G2, 2), (C3, 2),                              // 10
                (G2, 4),                                       // 11
                (C3, 2), (G2, 2),                              // 12
                (C3, 4), (G2, 4), (C3, 4), (G2, 2), (C3, 2));  // 13-16
        }

        [Test]
        public void TheLeftHandPlaysSingleNotesOnly()
        {
            foreach (string file in new[] { "TwinkleTwinkle.mid", "OdeToJoy.mid" })
            {
                List<SongNote> left = Hand(Load(file).Song, NoteHand.Left);
                for (int i = 1; i < left.Count; i++)
                    Assert.Greater(left[i].StartTime, left[i - 1].StartTime, $"{file} note {i}");
            }
        }

        [Test]
        public void TheOdeIsSixtyTwoNotesInTheRightHand()
        {
            Assert.AreEqual(62, Hand(Load("OdeToJoy.mid").Song, NoteHand.Right).Count);
        }

        [Test]
        public void TheMinuetLoadsAsWritten()
        {
            SongLoadReport r = Load("MinuetInG.mid");
            Assert.IsTrue(r.Ok, r.Error);
            SongData song = r.Song;

            Assert.AreEqual(HandsStatus.Named, song.Hands);
            Assert.AreEqual(120f, song.Bpm, 0.01f);
            Assert.AreEqual(3, song.BeatsPerBar);
            Assert.AreEqual(4, song.TempoMap.FirstBeatUnit);
            Assert.AreEqual(32, song.BarCount);
            Assert.IsFalse(song.TempoMap.BarLinesMayBeOff);

            Assert.AreEqual(129, Hand(song, NoteHand.Right).Count);
            Assert.AreEqual(75, Hand(song, NoteHand.Left).Count);
            Assert.AreEqual(0, Hand(song, NoteHand.Shared).Count, "no key is shared by both hands");
            Assert.AreEqual(0, r.NotesMoved);
            Assert.AreEqual(0, r.NotesMerged);
        }

        [Test]
        public void TheTwoStudyPassagesHaveTheirNoteCounts()
        {
            Assert.AreEqual(30, RightHandNotesInBars(Load("OdeToJoy.mid").Song, 1, 8), "Ode to Joy, bars 1-8");
            Assert.AreEqual(33, RightHandNotesInBars(Load("MinuetInG.mid").Song, 1, 8), "Minuet, bars 1-8");
        }

        [TestCase("TwinkleTwinkle.mid")]
        [TestCase("OdeToJoy.mid")]
        [TestCase("MinuetInG.mid")]
        public void TheImporterTurnsEachFileIntoAnAsset(string file)
        {
            var asset = AssetDatabase.LoadAssetAtPath<MidiSongAsset>("Assets/Songs/" + file);
            Assert.IsNotNull(asset, "imported by MidiImporter");
            Assert.AreEqual(file, asset.FileName);
            Assert.IsTrue(MidiSongLoader.Load(asset.Bytes, asset.FileName).Ok);
        }

        [Test]
        public void TheNotePaletteAssetKeepsTheRules()
        {
            var palette = AssetDatabase.LoadAssetAtPath<NotePalette>("Assets/UI/NotePalette.asset");
            Assert.IsNotNull(palette, "made by SceneSetup");
            Assert.IsNull(palette.FindClash());
        }
    }
}
