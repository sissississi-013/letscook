using NUnit.Framework;
using LetsCook.EditorTools;

namespace LetsCook.Tests
{
    public class TakeImporterTests
    {
        [Test]
        public void CanonicalNamesCoverRokokoAndMixamoStyles()
        {
            Assert.AreEqual("LeftFinger2Proximal", TakeImporter.Canonical("LeftFinger2Proximal"));
            Assert.AreEqual("RightFinger1Tip", TakeImporter.Canonical("Rokoko:RightFinger1Tip"));
            Assert.AreEqual("LeftHand", TakeImporter.Canonical("Newton:LeftHand"));
            Assert.AreEqual("RightFinger3Medial", TakeImporter.Canonical("mixamorig:RightHandMiddle2"));
            Assert.AreEqual("LeftFinger1Metacarpal", TakeImporter.Canonical("LeftHandThumb1"));
            Assert.IsNull(TakeImporter.Canonical("LeftForeArm"));
            Assert.IsNull(TakeImporter.Canonical("Hips"));
        }
    }
}
