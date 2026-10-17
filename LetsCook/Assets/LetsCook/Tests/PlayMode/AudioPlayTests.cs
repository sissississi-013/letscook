using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class AudioPlayTests
    {
        [UnityTest]
        public IEnumerator PanSizzlesOnlyWithFoodOnHeat()
        {
            var kitchen = Kitchen.Build();
            var audio = new GameObject("audio").AddComponent<KitchenAudio>();
            var pan = Object.FindFirstObjectByType<Cookware>();
            pan.temperature = 200f;
            yield return new WaitForSeconds(0.5f);
            Assert.Less(audio.SizzleLevel, 0.05f, "hot empty pan is quiet");

            var bread = Food.All.First(f => f.ingredient == "bread");
            var rb = bread.GetComponent<Rigidbody>();
            rb.position = pan.transform.position + new Vector3(0f, 0.03f, 0f);
            Food.TimeScale = 20f;
            yield return new WaitForSeconds(2f);
            Assert.Greater(audio.SizzleLevel, 0.2f, "bread on a hot pan sizzles: " + bread.Describe());

            Food.TimeScale = 1f;
            Object.Destroy(audio.gameObject);
            Object.Destroy(kitchen.gameObject);
        }
    }
}
