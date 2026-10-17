using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class OilPlayTests
    {
        [UnityTest]
        public IEnumerator PouringOilCoatsThePanAndItSmokesWhenTooHot()
        {
            var kitchen = Kitchen.Build();
            CookEvents.Clear();
            var pan = Object.FindFirstObjectByType<Cookware>();
            var bottle = Object.FindObjectsByType<Shaker>(FindObjectsSortMode.None).First(s => s.seasoning == "oil");
            var rb = bottle.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.position = pan.transform.position + Vector3.up * 0.25f;
            rb.rotation = Quaternion.Euler(150f, 0f, 0f);   // tipped well past sideways
            yield return new WaitForSeconds(2f);
            Assert.Greater(pan.oil, 3f, "about 4 g/s for 2 s");
            Assert.Greater(pan.ContactBoost, 1.2f);

            rb.position = new Vector3(0.7f, 1.3f, 0.62f);
            rb.rotation = Quaternion.identity;   // upright again: stop pouring
            float before = pan.oil;
            yield return new WaitForSeconds(0.5f);
            Assert.That(pan.oil, Is.EqualTo(before).Within(0.05f));

            pan.temperature = 245f;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(pan.Smoking);
            Assert.IsTrue(CookEvents.History.Any(e => e.type == "smoke"));
            Object.Destroy(kitchen.gameObject);
        }
    }
}
