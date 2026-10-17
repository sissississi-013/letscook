using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class CuttingPlayTests
    {
        GameObject root;

        [SetUp]
        public void SetUp() => root = new GameObject("cut test");

        [TearDown]
        public void TearDown() { Object.Destroy(root); foreach (var f in Food.All.ToList()) Object.Destroy(f.gameObject); }

        Food MakeTomato()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(1f, 0.02f, 1f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(root.transform);
            go.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);
            go.transform.position = new Vector3(0f, 0.041f, 0f);
            go.AddComponent<Rigidbody>().mass = 0.12f;
            var food = go.AddComponent<Food>();
            food.Init("tomato");
            return food;
        }

        KnifeEdge DropKnife(float speed)
        {
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(root.transform);
            blade.transform.localScale = new Vector3(0.003f, 0.035f, 0.18f);   // thin along x
            blade.transform.position = new Vector3(0f, 0.12f, 0f);
            blade.transform.rotation = Quaternion.LookRotation(Vector3.right);  // blade normal (forward) = world x
            var rb = blade.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = Vector3.down * speed;
            return blade.AddComponent<KnifeEdge>();
        }

        [UnityTest]
        public IEnumerator FastChopSplitsFoodInHalf()
        {
            var tomato = MakeTomato();
            yield return new WaitForFixedUpdate();
            DropKnife(1.2f);
            yield return new WaitForSeconds(0.3f);

            var pieces = Food.All.Where(f => f.ingredient == "tomato").ToList();
            Assert.AreEqual(2, pieces.Count, "one chop should make two pieces");
            Assert.That(pieces.All(p => p.cut == "halves"));
            Assert.That(pieces[0].GetComponent<Rigidbody>().mass, Is.EqualTo(0.06f).Within(0.001f));
            Assert.That(pieces[0].transform.localScale.x, Is.EqualTo(0.04f).Within(0.001f), "split across the blade normal (x)");
            Assert.AreNotSame(pieces[0].state, pieces[1].state);
            Assert.IsTrue(CookEvents.History.Any(e => e.type == "cut" && e.subject == "tomato"));
        }

        [UnityTest]
        public IEnumerator GentleTouchDoesNotCut()
        {
            MakeTomato();
            yield return new WaitForFixedUpdate();
            DropKnife(0.1f);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1, Food.All.Count(f => f.ingredient == "tomato"));
        }
    }
}
