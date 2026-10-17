using NUnit.Framework;

namespace LetsCook.Tests
{
    public class FoodStateTests
    {
        static FoodState Cook(string ingredient, float? contact, float seconds, float dt = 0.02f)
        {
            var s = new FoodState(IngredientProfile.For(ingredient));
            for (float t = 0; t < seconds; t += dt) s.Step(dt, contact);
            return s;
        }

        [Test]
        public void NoHeatStaysRaw()
        {
            var s = Cook("bread", null, 300f);
            Assert.AreEqual("raw", s.Doneness);
            Assert.AreEqual(0f, s.browning);
        }

        [Test]
        public void WaterPinsSurfaceAtBoilingUntilDry()
        {
            var tomato = Cook("tomato", 220f, 20f);
            Assert.That(tomato.surfaceTemp, Is.EqualTo(100f).Within(0.5f));
            Assert.AreEqual(0f, tomato.browning, "wet food should not brown yet");
        }

        [Test]
        public void BreadToastsGoldenInAboutTwoMinutes()
        {
            Assert.Less(Cook("bread", 200f, 30f).browning, 0.4f);
            var toast = Cook("bread", 200f, 150f);
            Assert.That(toast.browning, Is.InRange(0.4f, 0.9f));
            Assert.Less(toast.charred, 0.2f);
        }

        [Test]
        public void VeryHotPanBurnsBreadEventually()
        {
            var s = Cook("bread", 260f, 600f);
            Assert.Greater(s.charred, 0.6f);
            Assert.AreEqual("burnt", s.Doneness);
        }

        [Test]
        public void EggSetsOnMediumHeat()
        {
            var s = Cook("egg", 160f, 240f);
            Assert.Greater(s.coreTemp, 62f);
            Assert.Greater(s.set, 0.9f);
            StringAssert.StartsWith("fully set", s.Doneness);
        }

        [Test]
        public void ButterMeltsQuicklyInAWarmPan()
        {
            var s = Cook("butter", 120f, 30f);
            Assert.Greater(s.melted, 0.5f);
        }

        [Test]
        public void ColourDarkensWithBrowning()
        {
            var raw = new FoodState(IngredientProfile.For("bread"));
            var toast = Cook("bread", 220f, 200f);
            Assert.Less(toast.Color.grayscale, raw.Color.grayscale - 0.1f);
        }
    }
}
