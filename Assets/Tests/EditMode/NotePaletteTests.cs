using NUnit.Framework;
using UnityEngine;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class NotePaletteTests
    {
        private NotePalette _palette;

        [SetUp]
        public void SetUp() => _palette = ScriptableObject.CreateInstance<NotePalette>();

        [TearDown]
        public void TearDown()
        {
            NotePalette.Active = null;
            Object.DestroyImmediate(_palette);
        }

        [Test]
        public void TheDefaultColoursAreFarEnoughApart()
        {
            Assert.IsNull(_palette.FindClash());
        }

        [Test]
        public void ADifferentThemeThatMimicsAnOutcomeColourIsCaught()
        {
            _palette.leftHand = _palette.hit;
            StringAssert.Contains("left hand is too close to hit", _palette.FindClash());
        }

        [Test]
        public void TwoHandsTooCloseAreCaught()
        {
            _palette.rightHand = new Color(_palette.leftHand.r + 0.05f, _palette.leftHand.g, _palette.leftHand.b);
            StringAssert.Contains("left hand is too close to right hand", _palette.FindClash());
        }

        [Test]
        public void HandsAndAllNotesGetTheirOwnColours()
        {
            Assert.AreEqual(_palette.leftHand, _palette.SolidColor(PracticeHand.Left));
            Assert.AreEqual(_palette.rightHand, _palette.SolidColor(PracticeHand.Right));
            Assert.AreEqual(_palette.allNotes, _palette.SolidColor(PracticeHand.All));
            Assert.AreEqual(_palette.leftHand, _palette.OutlineColor(NoteHand.Left));
            Assert.AreEqual(_palette.rightHand, _palette.OutlineColor(NoteHand.Right));
        }

        [Test]
        public void OutcomeTintReadsTheActivePalette()
        {
            _palette.hit = Color.magenta;
            NotePalette.Active = _palette;
            Assert.AreEqual(Color.magenta, OutcomeTint.Hit);

            NotePalette.Active = null;
            Assert.AreEqual(NotePalette.DefaultHit, OutcomeTint.Hit);
        }
    }
}
