using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class ResetPlayTests
    {
        [UnityTest]
        public IEnumerator ResetGivesAFreshKitchen()
        {
            var kitchen = Kitchen.Build();
            var rig = new GameObject("rig");
            var reset = rig.AddComponent<KitchenReset>();
            var plating = rig.AddComponent<Plating>();
            plating.plate = kitchen.Find("Plate");
            plating.chefUrl = "http://127.0.0.1:8799/dish";

            // Mess things up: burn the bread, spawn an extra tool
            var bread = Food.All.First(f => f.ingredient == "bread");
            bread.state.charred = 1f;
            ToolBuilder.Build(new ToolSpec { name = "ladle", mass_kg = 0.1f, abilities = new string[0], parts = new ToolPart[0] }, Vector3.up);
            yield return null;

            var fresh = reset.ResetKitchen();
            yield return null; yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "Kitchen"));
            Assert.AreEqual(0, Object.FindObjectsByType<ToolInfo>(FindObjectsSortMode.None).Length, "requested tools are cleared");
            var newBread = Food.All.Single(f => f.ingredient == "bread");
            Assert.AreEqual(0f, newBread.state.charred);
            Assert.AreEqual(fresh.Find("Plate"), plating.plate);
            Assert.IsTrue(CookEvents.History.Any(e => e.type == "reset"));
            Object.Destroy(fresh.gameObject);
            Object.Destroy(rig);
        }
    }
}
