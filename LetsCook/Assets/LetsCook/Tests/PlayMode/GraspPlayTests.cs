using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class GraspPlayTests
    {
        static GloveFrame Pose(string name) =>
            GloveFrame.Parse(Resources.Load<TextAsset>("MockFrames/mock_" + name).text);

        static IEnumerator Fixed(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator PinchGrabsLiftsAndReleases()
        {
            var root = new GameObject("test root");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(root.transform);
            floor.transform.localScale = new Vector3(1f, 0.02f, 1f);
            floor.transform.position = new Vector3(0f, 0.912f, 0.4f);   // top at 0.922

            var anchor = new GameObject("anchor").transform;
            anchor.SetParent(root.transform);
            anchor.position = new Vector3(-0.22f, 1.0f, 0.35f);

            // A 3 cm cube where the left thumb and index meet in the mock pinch
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(root.transform);
            cube.transform.localScale = Vector3.one * 0.03f;
            cube.transform.position = anchor.position + new Vector3(0.031f, -0.063f, 0.0835f);
            cube.AddComponent<Rigidbody>().mass = 0.1f;
            cube.AddComponent<KitchenItem>().displayName = "test cube";

            var rx = new GameObject("rx").AddComponent<RokokoReceiver>();
            rx.transform.SetParent(root.transform);
            rx.mockWhenSilent = false;
            rx.port = 15046;
            rx.Inject(Pose("open"));

            var handGo = new GameObject("Left Hand");
            handGo.SetActive(false);
            handGo.transform.SetParent(root.transform);
            var hand = handGo.AddComponent<GloveHand>();
            hand.side = "Left";
            hand.receiver = rx;
            hand.anchor = anchor;
            var grasp = handGo.AddComponent<HandGrasp>();
            handGo.SetActive(true);

            yield return Fixed(10);
            Assert.IsNull(grasp.Held, "open hand should not hold anything");

            rx.Inject(Pose("pinch"));
            yield return Fixed(5);
            Assert.AreEqual(cube.GetComponent<Rigidbody>(), grasp.Held, "pinch should grab the cube");
            Assert.IsTrue(grasp.Assisted);

            float y0 = cube.transform.position.y;
            for (int i = 0; i < 30; i++) { anchor.position += Vector3.up * 0.004f; yield return new WaitForFixedUpdate(); }
            Assert.Greater(cube.transform.position.y, y0 + 0.08f, "cube should rise with the hand");

            rx.Inject(Pose("open"));
            yield return Fixed(5);
            Assert.IsNull(grasp.Held, "opening the hand should release");
            float yRelease = cube.transform.position.y;
            yield return Fixed(20);
            Assert.Less(cube.transform.position.y, yRelease - 0.02f, "released cube should fall");

            Object.Destroy(root);
        }
    }
}
