using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// SlabGeometry.VerticalExtent is the only place a note's on-screen size
    /// comes from, and from the headset there's no way to tell by eye whether
    /// a slab's height actually tracks its note's duration. These tests check
    /// the maths directly instead of settling that by squinting at the wall.
    /// </summary>
    public class SlabGeometryTests
    {
        [Test]
        public void TwoBeatNoteIsTwiceTheHeightOfAOneBeatNoteAtTheSamePointInItsDescent()
        {
            // Same start time and song time -> same bottom edge -> both slabs
            // are at an identical point in their descent, still fully above
            // the key line. highwayHeight is generous so neither clips.
            const double songTime = 0.0;
            const double startTime = 5.0;
            const float speed = 1f;
            const float highwayHeight = 10f;

            var (_, oneBeatHeight) = SlabGeometry.VerticalExtent(startTime, startTime + 1.0, songTime, speed, highwayHeight);
            var (_, twoBeatHeight) = SlabGeometry.VerticalExtent(startTime, startTime + 2.0, songTime, speed, highwayHeight);

            Assert.AreEqual(2f * oneBeatHeight, twoBeatHeight, 1e-6f);
        }

        [Test]
        public void NoteNotYetDueIsHidden()
        {
            // Due in 10s; the wall only shows 5s of lead time.
            var (_, height) = SlabGeometry.VerticalExtent(10.0, 11.0, 0.0, speed: 1f, highwayHeight: 5f);
            Assert.LessOrEqual(height, 0f);
        }

        [Test]
        public void NoteFullyPastIsHidden()
        {
            // Ended 4s ago.
            var (_, height) = SlabGeometry.VerticalExtent(0.0, 1.0, 5.0, speed: 1f, highwayHeight: 5f);
            Assert.LessOrEqual(height, 0f);
        }

        [Test]
        public void NoteBeingPlayedHasItsBottomPinnedAtZeroAndItsHeightShrinking()
        {
            const double startTime = 0.0;
            const double endTime = 2.0; // a 2-second note
            const float speed = 1f;
            const float highwayHeight = 5f;

            var (bottomEarly, heightEarly) = SlabGeometry.VerticalExtent(startTime, endTime, 0.5, speed, highwayHeight);
            var (bottomLate, heightLate) = SlabGeometry.VerticalExtent(startTime, endTime, 1.0, speed, highwayHeight);

            Assert.AreEqual(0f, bottomEarly, 1e-6f);
            Assert.AreEqual(0f, bottomLate, 1e-6f);
            Assert.Less(heightLate, heightEarly);
        }

        [Test]
        public void NoteLongerThanTheWholeWindowIsClippedToHighwayHeight()
        {
            const float highwayHeight = 5f;
            // A 100-second note, just starting -- far longer than the window
            // highwayHeight/speed can ever show at once.
            var (bottom, height) = SlabGeometry.VerticalExtent(0.0, 100.0, 0.0, speed: 1f, highwayHeight: highwayHeight);

            Assert.AreEqual(0f, bottom, 1e-6f);
            Assert.AreEqual(highwayHeight, height, 1e-6f);
        }

        [Test]
        public void NegativeSongTimeDuringTheLeadInHidesEverything()
        {
            // Mirrors SongClock's real lead-in: highwayHeight = 0.5, leadTime
            // = 3s -> speed = highwayHeight / leadTimeSeconds. The first
            // note starts at songTime 0. At songTime == -leadTimeSeconds --
            // the instant the lead-in begins -- the note should just be
            // touching the top of the wall (height == 0), and any more
            // negative songTime should leave it further above that, still
            // hidden. No special-casing of the sign is needed: bottom and
            // top are affine in songTime, so the same two clamps that hide a
            // not-yet-due note handle this for free.
            const float highwayHeight = 0.5f;
            const float leadTimeSeconds = 3f;
            const float speed = highwayHeight / leadTimeSeconds;

            double[] negativeSongTimes = { -leadTimeSeconds, -leadTimeSeconds - 0.001, -7.0, -1000.0 };
            foreach (double songTime in negativeSongTimes)
            {
                var (_, height) = SlabGeometry.VerticalExtent(0.0, 0.5, songTime, speed, highwayHeight);
                Assert.LessOrEqual(height, 1e-6f, $"songTime={songTime}");
            }
        }
    }
}
