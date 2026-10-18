using System.Collections;
using System.Net.Sockets;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class ReceiverPlayTests
    {
        [UnityTest]
        public IEnumerator ReceivesUdpFrameAndGoesLive()
        {
            var go = new GameObject("rx");
            go.SetActive(false);
            var rx = go.AddComponent<RokokoReceiver>();
            rx.port = 15045;
            go.SetActive(true);
            Assert.IsFalse(rx.IsLive);
            Assert.IsTrue(rx.DrivesPhysics, "without gloves the mouse hands are solid");
            Assert.AreEqual("mouse", rx.Latest.source);
            Assert.AreEqual(50, rx.Latest.joints.Count);
            Assert.Less(HandMath.Curl(rx.Latest, "Right", 2), 0.1f, "hands rest open");

            var json = Resources.Load<TextAsset>("MockFrames/mock_fist").text;
            using (var udp = new UdpClient())
            {
                udp.Client.SendBufferSize = 65535;
                var bytes = Encoding.UTF8.GetBytes(json);
                for (int i = 0; i < 5 && !rx.IsLive; i++)
                {
                    udp.Send(bytes, bytes.Length, "127.0.0.1", 15045);
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }
            Assert.IsTrue(rx.IsLive, "receiver never went live");
            Assert.AreEqual(50, rx.Latest.joints.Count);
            Assert.Greater(HandMath.Curl(rx.Latest, "Left", 3), 0.9f);
            Object.Destroy(go);
        }
    }
}
