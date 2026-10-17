using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class EggPlayTests
    {
        Transform kitchen;

        [SetUp] public void SetUp() { kitchen = Kitchen.Build(); CookEvents.Clear(); }
        [TearDown] public void TearDown() { Object.Destroy(kitchen.gameObject); Food.TimeScale = 1f; }

        static Food Egg() => Food.All.First(f => f.ingredient == "egg");

        [UnityTest]
        public IEnumerator GentleSetDownDoesNotCrack()
        {
            var egg = Egg();
            var rb = egg.GetComponent<Rigidbody>();
            rb.position = new Vector3(0.1f, Kitchen.CounterTop + 0.035f, 0.35f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual("whole", Egg().cut);
        }

        [UnityTest]
        public IEnumerator DroppedIntoThePanItCracksAndFries()
        {
            var pan = Object.FindFirstObjectByType<Cookware>();
            var egg = Egg();
            var rb = egg.GetComponent<Rigidbody>();
            rb.position = pan.transform.position + Vector3.up * 0.25f;   // about 2.2 m/s at impact
            yield return new WaitForSeconds(1f);

            var contents = Food.All.FirstOrDefault(f => f.ingredient == "egg" && f.cut == "cracked");
            Assert.NotNull(contents, "egg should crack");
            Assert.IsFalse(Food.All.Any(f => f.ingredient == "egg" && f.cut == "whole"), "shell egg should be gone");
            Assert.IsTrue(CookEvents.History.Any(e => e.type == "crack" && e.detail == "into the pan"),
                "crack event should say it landed in the pan: " + string.Join(",", CookEvents.History.Select(e => e.type + ":" + e.detail)));

            Food.TimeScale = 40f;
            yield return new WaitForSeconds(5f);
            StringAssert.StartsWith("fully set", contents.state.Doneness, contents.Describe());
        }
    }
}
