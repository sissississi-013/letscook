using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class ConsentPlayTests
    {
        [UnityTest]
        public IEnumerator NothingIsRecordedUntilThePlayerAgrees()
        {
            var go = new GameObject("rig");
            go.SetActive(false);
            var logger = go.AddComponent<SessionLogger>();
            logger.enabled = false;   // as saved in the scene
            logger.directory = System.IO.Path.Combine(Application.temporaryCachePath, "consent-test");
            var consent = go.AddComponent<Consent>();
            consent.logger = logger;
            go.SetActive(true);
            yield return null;
            Assert.IsFalse(logger.enabled, "logger must wait for consent");
            Assert.IsFalse(consent.Answer.HasValue);
            Assert.IsNull(logger.FilePath, "no session file before consent");

            consent.Decide(false);
            yield return null;
            Assert.IsFalse(logger.enabled, "declining keeps it off");

            consent.Decide(true);
            yield return null;
            Assert.IsTrue(logger.enabled);
            Assert.IsNotNull(logger.FilePath);
            Object.Destroy(go);
        }
    }
}
