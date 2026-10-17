using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class PlatingPlayTests
    {
        [UnityTest]
        public IEnumerator ServingSendsPlateAndShowsADish()
        {
            var kitchen = Kitchen.Build();
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 1.5f, -0.25f);
            cam.transform.LookAt(new Vector3(0, 0.9f, 0.5f));
            var plating = new GameObject("plating").AddComponent<Plating>();
            plating.plate = GameObject.Find("Plate").transform;
            plating.shotCamera = cam;
            plating.chefUrl = "http://127.0.0.1:8799/dish";   // nothing listens here → offline card

            var bread = Food.All.First(f => f.ingredient == "bread");
            var rb = bread.GetComponent<Rigidbody>();   // interpolated bodies ignore transform writes
            rb.position = plating.plate.position + Vector3.up * 0.03f;
            rb.linearVelocity = Vector3.zero;
            bread.state.browning = 0.5f;
            yield return new WaitForSeconds(0.5f);

            var onPlate = plating.FoodOnPlate();
            Assert.AreEqual(1, onPlate.Count, $"bread at {bread.transform.position}, plate at {plating.plate.position}, foods {Food.All.Count}");
            string summary = plating.SummaryJson(onPlate);
            StringAssert.Contains("\"ingredient\":\"bread\"", summary);
            StringAssert.Contains("golden brown", summary);
            StringAssert.Contains("\"left_in_kitchen\":[", summary);

            LogAssert.ignoreFailingMessages = true;
            plating.Serve();
            float until = Time.realtimeSinceStartup + 10f;
            while (plating.Busy || plating.LastDish == null)
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("serve never finished");
                yield return null;
            }
            Assert.IsTrue(plating.LastDish.offline);
            Assert.IsTrue(CookEvents.History.Any(e => e.type == "plate"));

            Object.Destroy(kitchen.gameObject);
            Object.Destroy(cam.gameObject);
            Object.Destroy(plating.gameObject);
        }
    }
}
