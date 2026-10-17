using NUnit.Framework;
using UnityEngine;

namespace LetsCook.Tests
{
    public class GloveFrameTests
    {
        static GloveFrame Load(string pose) =>
            GloveFrame.Parse(Resources.Load<TextAsset>("MockFrames/mock_" + pose).text);

        [Test]
        public void ParsesAllHandJoints()
        {
            var f = Load("open");
            Assert.NotNull(f);
            Assert.AreEqual(50, f.joints.Count);
            Assert.AreEqual("Mock Newton", f.source);
            Assert.IsTrue(f.TryGet("RightFinger5Tip", out var tip));
            Assert.AreEqual("RightFinger5Distal", tip.parent);
            Assert.IsTrue(f.TryGet("LeftHand", out var hand));
            Assert.IsNull(hand.parent);
        }

        [Test]
        public void RejectsV3AndGarbage()
        {
            Assert.IsNull(GloveFrame.Parse("{\"scene\":{\"actors\":[{\"name\":\"a\"}],\"newtons\":[]}}"));
            Assert.IsNull(GloveFrame.Parse("not json"));
        }

        [Test]
        public void CurlMatchesPoses()
        {
            Assert.Less(HandMath.Curl(Load("open"), "Left", 2), 0.05f);
            Assert.Greater(HandMath.Curl(Load("fist"), "Left", 2), 0.9f);
            var point = Load("point");
            Assert.Less(HandMath.Curl(point, "Right", 2), 0.05f);
            Assert.Greater(HandMath.Curl(point, "Right", 3), 0.9f);
            float pinchIndex = HandMath.Curl(Load("pinch"), "Left", 2);
            Assert.That(pinchIndex, Is.InRange(0.4f, 0.8f));
        }

        [Test]
        public void LerpBlendsHalfway()
        {
            var a = Load("open");
            var b = Load("fist");
            var mid = GloveFrame.Lerp(a, b, 0.5f);
            a.TryGet("LeftFinger2Tip", out var pa);
            b.TryGet("LeftFinger2Tip", out var pb);
            mid.TryGet("LeftFinger2Tip", out var pm);
            Assert.That(Vector3.Distance(pm.position, (pa.position + pb.position) * 0.5f), Is.LessThan(1e-4f));
        }
    }
}
