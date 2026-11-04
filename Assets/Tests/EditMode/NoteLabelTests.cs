using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class NoteLabelTests
    {
        [TestCase(60, "C")]
        [TestCase(61, "C♯")]
        [TestCase(62, "D")]
        [TestCase(63, "D♯")]
        [TestCase(64, "E")]
        [TestCase(65, "F")]
        [TestCase(66, "F♯")]
        [TestCase(67, "G")]
        [TestCase(68, "G♯")]
        [TestCase(69, "A")]
        [TestCase(70, "A♯")]
        [TestCase(71, "B")]
        public void NamesEveryPitchClassWithSharps(int pitch, string expected)
        {
            Assert.AreEqual(expected, NoteLabel.Name(pitch));
        }

        [Test]
        public void OctaveDoesNotChangeTheName()
        {
            Assert.AreEqual("A", NoteLabel.Name(21));   // A0, lowest key
            Assert.AreEqual("C", NoteLabel.Name(108));  // C8, highest key
            Assert.AreEqual(NoteLabel.Name(61), NoteLabel.Name(61 + 24));
        }

        [Test]
        public void AsciiFallbackUsesHash()
        {
            Assert.AreEqual("F#", NoteLabel.Name(66, sharpGlyph: false));
            Assert.AreEqual("E", NoteLabel.Name(64, sharpGlyph: false));
        }

        [Test]
        public void NegativePitchStillNamed()
        {
            Assert.AreEqual("B", NoteLabel.Name(-1));
            Assert.AreEqual("C", NoteLabel.Name(-12));
        }

        [Test]
        public void FitsNeedsLabelPlusPaddingBothSides()
        {
            Assert.IsTrue(NoteLabel.Fits(0.020f, 0.010f, 0.005f));
            Assert.IsFalse(NoteLabel.Fits(0.0199f, 0.010f, 0.005f));
        }

        [Test]
        public void HiddenSlabNeverFits()
        {
            Assert.IsFalse(NoteLabel.Fits(0f, 0f, 0f));
            Assert.IsFalse(NoteLabel.Fits(-0.1f, 0.01f, 0f));
        }

        [Test]
        public void NegativePaddingTreatedAsZero()
        {
            Assert.IsTrue(NoteLabel.Fits(0.010f, 0.010f, -1f));
            Assert.AreEqual(0.005f, NoteLabel.CentreAboveBottom(0.010f, -1f), 1e-6f);
        }

        [Test]
        public void LabelCentreSitsPaddingPlusHalfHeightAboveBottom()
        {
            Assert.AreEqual(0.009f, NoteLabel.CentreAboveBottom(0.008f, 0.005f), 1e-6f);
        }
    }
}
