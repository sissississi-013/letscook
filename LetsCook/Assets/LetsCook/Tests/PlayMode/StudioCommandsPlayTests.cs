using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class StudioCommandsPlayTests
    {
        [UnityTest]
        public IEnumerator UnreachableStudioFailsGracefully()
        {
            var go = new GameObject("studio");
            var cmd = go.AddComponent<StudioCommands>();
            cmd.baseUrl = "http://127.0.0.1:14999/v1/1234";   // nothing listens here
            LogAssert.ignoreFailingMessages = true;
            yield return cmd.Calibrate();
            Assert.IsNull(cmd.LastResponse);
            Assert.IsFalse(cmd.Busy);
            Object.Destroy(go);
        }
    }
}
