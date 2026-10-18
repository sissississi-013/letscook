using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class MouseGrabPlayTests
    {
        static GloveFrame Pose(string name) => GloveFrame.Parse(Resources.Load<TextAsset>("MockFrames/mock_" + name).text);

        /// Mouse mode puts the right wrist so its pinch point sits under the cursor at the default hand height.
        /// With the cursor on the kitchen tomato, closing the hand must pick it up.
        [UnityTest]
        public IEnumerator PinchUnderTheCursorPicksUpTheTomato()
        {
            var kitchen = Kitchen.Build();
            yield return new WaitForSeconds(0.5f);   // let things settle
            var tomato = Food.All.First(f => f.ingredient == "tomato");

            var anchor = new GameObject("right anchor").transform;
            Vector3 pinchTarget = tomato.transform.position;
            anchor.position = new Vector3(pinchTarget.x - WristController.RightPinchOffset.x, 1.0f, pinchTarget.z - WristController.RightPinchOffset.z);

            var rx = new GameObject("rx").AddComponent<RokokoReceiver>();
            rx.port = 15049;
            rx.Inject(Pose("open"));
            var go = new GameObject("Right Hand");
            go.SetActive(false);
            var hand = go.AddComponent<GloveHand>();
            hand.side = "Right"; hand.receiver = rx; hand.anchor = anchor;
            var grasp = go.AddComponent<HandGrasp>();
            go.SetActive(true);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.IsNull(grasp.Held);

            rx.Inject(Pose("pinch"));
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual("tomato", grasp.HeldName, "pinch at the default height over the tomato should grab it");

            Object.Destroy(go); Object.Destroy(rx.gameObject); Object.Destroy(anchor.gameObject); Object.Destroy(kitchen.gameObject);
        }
    }
}
