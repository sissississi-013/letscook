using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class ShakerPlayTests
    {
        Transform kitchen;
        [SetUp] public void SetUp() => kitchen = Kitchen.Build();
        [TearDown] public void TearDown() => Object.Destroy(kitchen.gameObject);

        static (Shaker shaker, Rigidbody rb) Salt()
        {
            var s = Object.FindObjectsByType<Shaker>(FindObjectsSortMode.None).First(x => x.seasoning == "salt" && x.gameObject.scene.IsValid());
            return (s, s.GetComponent<Rigidbody>());
        }

        [UnityTest]
        public IEnumerator UprightShakerDispensesNothing()
        {
            var (shaker, _) = Salt();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(0f, shaker.GramsDispensed);
        }

        [UnityTest]
        public IEnumerator ShakingUpsideDownOverBreadSaltsIt()
        {
            var bread = kitchen.GetComponentsInChildren<Food>().First(f => f.ingredient == "bread");   // not a leftover from another test
            var (shaker, rb) = Salt();
            rb.isKinematic = true;   // hold it like a hand would
            rb.position = bread.transform.position + Vector3.up * 0.2f;
            rb.rotation = Quaternion.Euler(180f, 0f, 0f);
            for (int i = 0; i < 60; i++)
            {
                rb.linearVelocity = Vector3.up * (i % 2 == 0 ? 0.6f : -0.6f);   // shake
                yield return new WaitForFixedUpdate();
            }
            Assert.Greater(shaker.GramsDispensed, 0.1f);
            Assert.Greater(bread.state.salt, 0.1f, bread.Describe());
            StringAssert.Contains("salted", bread.Describe());
            Assert.AreEqual(0f, bread.state.pepper);
        }
    }
}
