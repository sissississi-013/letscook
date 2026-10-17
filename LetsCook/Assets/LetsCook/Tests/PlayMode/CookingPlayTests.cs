using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class CookingPlayTests
    {
        [UnityTest]
        public IEnumerator BreadInPanOnBurnerToasts()
        {
            Food.TimeScale = 40f;
            var kitchen = Kitchen.Build();
            yield return new WaitForFixedUpdate();

            var pan = Object.FindFirstObjectByType<Cookware>();
            var bread = Food.All.First(f => f.ingredient == "bread");
            var butter = Food.All.First(f => f.ingredient == "butter");
            bread.transform.position = pan.transform.position + new Vector3(-0.02f, 0.03f, 0f);
            butter.transform.position = pan.transform.position + new Vector3(0.06f, 0.04f, 0.04f);

            yield return new WaitForSeconds(5f);   // about 200 s of cooking time
            Assert.Greater(pan.temperature, 150f, "pan should preheat on the lit burner");
            Assert.Greater(bread.state.browning, 0.2f, "bread touching the hot pan should brown: " + bread.Describe());
            Assert.Greater(butter.state.melted, 0.5f, "butter should melt: " + butter.Describe());

            var tomato = Food.All.First(f => f.ingredient == "tomato");
            Assert.AreEqual("raw", tomato.state.Doneness, "tomato on the board stays raw");

            Food.TimeScale = 1f;
            Object.Destroy(kitchen.gameObject);
        }
    }
}
