using NUnit.Framework;
using PianoGlass.Scoring;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// Attempt schema v2: the new practice fields round-trip, and every v1
    /// file already on the headset (the 2026-10-29 Twinkle attempts) still
    /// loads, reading as what it was -- continuous, no loop, both hands, no
    /// metronome.
    /// </summary>
    public class AttemptSchemaTests
    {
        /// <summary>A v1 attempt exactly as ScoreKeeper wrote them before v2:
        /// every v1 field, none of the v2 ones. Trimmed to two notes.</summary>
        private const string V1 = @"{
    ""schema"": 1,
    ""song"": ""Twinkle, Twinkle, Little Star"",
    ""bpm"": 120.0,
    ""startedUtc"": ""2026-10-29T19:02:11.0000000Z"",
    ""endedUtc"": ""2026-10-29T19:02:47.0000000Z"",
    ""outcome"": ""completed"",
    ""tempo"": 1.0,
    ""inputOffsetMs"": 0.0,
    ""displayLatencyMs"": 31.2,
    ""windows"": { ""perfectMs"": 40.0, ""greatMs"": 80.0, ""goodMs"": 150.0 },
    ""score"": 1900,
    ""accuracy"": 95.0,
    ""perfect"": 1, ""great"": 1, ""good"": 0, ""miss"": 0,
    ""notesHit"": 2, ""notesMissed"": 0,
    ""wrongPitch"": 0, ""extra"": 0,
    ""meanErrorMs"": -12.0,
    ""errorSdMs"": 20.0,
    ""notes"": [
        { ""noteIndex"": 0, ""pitch"": 60, ""bar"": 1, ""expectedTime"": 0.0, ""pressed"": true,
          ""pressTime"": -0.032, ""errorMs"": -32.0, ""judgement"": 0, ""rating"": ""Perfect"" },
        { ""noteIndex"": 1, ""pitch"": 60, ""bar"": 1, ""expectedTime"": 0.5, ""pressed"": true,
          ""pressTime"": 0.508, ""errorMs"": 8.0, ""judgement"": 1, ""rating"": ""Great"" }
    ],
    ""wrongNotes"": [],
    ""bars"": [ { ""bar"": 1, ""notes"": 2, ""perfect"": 1, ""great"": 1, ""good"": 0, ""miss"": 0,
                  ""wrongNotes"": 0, ""accuracy"": 95.0 } ]
}";

        [Test]
        public void V1FileLoadsWithItsOwnSchemaNumber()
        {
            AttemptRecord rec = AttemptRecord.Parse(V1);
            Assert.IsNotNull(rec);
            Assert.AreEqual(1, rec.schema, "a reader must still be able to tell it is v1");
        }

        [Test]
        public void V1FileReadsAsContinuousBothHandsNoLoopNoMetronome()
        {
            AttemptRecord rec = AttemptRecord.Parse(V1);
            Assert.AreEqual(AttemptRecord.Continuous, rec.mode);
            Assert.AreEqual(AttemptRecord.NoLoop, rec.loopFromBar);
            Assert.AreEqual(AttemptRecord.NoLoop, rec.loopToBar);
            Assert.IsFalse(rec.IsLoop);
            Assert.AreEqual(AttemptRecord.BothHands, rec.hand);
            Assert.IsFalse(rec.metronome);
        }

        [Test]
        public void V1FileKeepsItsResults()
        {
            AttemptRecord rec = AttemptRecord.Parse(V1);
            Assert.AreEqual("Twinkle, Twinkle, Little Star", rec.song);
            Assert.AreEqual(1.0, rec.tempo, 1e-9);
            Assert.AreEqual(95.0, rec.accuracy, 1e-9);
            Assert.AreEqual(2, rec.notes.Count);
            Assert.AreEqual(Judgement.Great, rec.notes[1].judgement);
            Assert.AreEqual(150f, rec.windows.goodMs);
            Assert.AreEqual(1, rec.bars.Count);
            Assert.AreEqual(0, rec.wrongNotes.Count);
        }

        [Test]
        public void NewRecordIsV2WithDefaults()
        {
            var rec = new AttemptRecord();
            Assert.AreEqual(2, rec.schema);
            Assert.AreEqual(AttemptRecord.CurrentSchema, rec.schema);
            Assert.AreEqual("continuous", rec.mode);
            Assert.AreEqual(-1, rec.loopFromBar);
            Assert.AreEqual(-1, rec.loopToBar);
            Assert.AreEqual("both", rec.hand);
            Assert.IsFalse(rec.metronome);
        }

        [Test]
        public void V2FieldsRoundTrip()
        {
            var rec = new AttemptRecord
            {
                song = "x", mode = AttemptRecord.Wait, loopFromBar = 3, loopToBar = 5,
                hand = AttemptRecord.BothHands, metronome = true,
            };
            AttemptRecord back = AttemptRecord.Parse(JsonUtility.ToJson(rec, true));

            Assert.AreEqual(2, back.schema);
            Assert.AreEqual("wait", back.mode);
            Assert.AreEqual(3, back.loopFromBar);
            Assert.AreEqual(5, back.loopToBar);
            Assert.IsTrue(back.IsLoop);
            Assert.IsTrue(back.metronome);
        }

        [Test]
        public void V2JsonNamesTheNewFields()
        {
            string json = JsonUtility.ToJson(new AttemptRecord());
            StringAssert.Contains("\"schema\":2", json);
            StringAssert.Contains("\"mode\":\"continuous\"", json);
            StringAssert.Contains("\"loopFromBar\":-1", json);
            StringAssert.Contains("\"loopToBar\":-1", json);
            StringAssert.Contains("\"hand\":\"both\"", json);
            StringAssert.Contains("\"metronome\":false", json);
        }

        [Test]
        public void EmptyOrHalfALoopFallsBackToDefaults()
        {
            AttemptRecord rec = AttemptRecord.Parse(
                "{\"schema\":2,\"mode\":\"\",\"hand\":\"\",\"loopFromBar\":4,\"loopToBar\":-1}");
            Assert.AreEqual(AttemptRecord.Continuous, rec.mode);
            Assert.AreEqual(AttemptRecord.BothHands, rec.hand);
            Assert.IsFalse(rec.IsLoop, "a loop needs both ends");
            Assert.AreEqual(AttemptRecord.NoLoop, rec.loopFromBar);
            Assert.IsNotNull(rec.notes);
        }

        [Test]
        public void GarbageIsNull()
        {
            Assert.IsNull(AttemptRecord.Parse(""));
            Assert.IsNull(AttemptRecord.Parse("not json"));
        }
    }
}
