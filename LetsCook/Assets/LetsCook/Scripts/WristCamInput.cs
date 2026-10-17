using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LetsCook
{
    /// Receives wrist positions from wrist_cam.py (UDP 14046, game coordinates) and glides the wrist anchors there.
    /// While packets are fresh the camera drives the hands; otherwise the keyboard (WristController) does.
    public class WristCamInput : MonoBehaviour
    {
        public int port = 14046;
        public WristController wrists;
        public float smoothing = 14f;      // 1/s
        public float staleSeconds = 0.5f;

        [Serializable] class Hand { public bool ok; public float[] pos; }
        [Serializable] class Hands { public Hand Left, Right; }
        [Serializable] class Packet { public double t; public Hands hands; }

        UdpClient client;
        Thread thread;
        volatile bool running;
        readonly object gate = new object();
        Packet latest;
        long lastTicks;

        public bool Active => latest != null && (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastTicks)) < TimeSpan.FromSeconds(staleSeconds).Ticks;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            try { client = new UdpClient(port); }
            catch (SocketException e) { Debug.LogWarning($"[let's cook] wrist cam can't listen on {port}: {e.Message}"); return; }
            running = true;
            thread = new Thread(Loop) { IsBackground = true, Name = "WristCam" };
            thread.Start();
        }

        void OnDisable()
        {
            running = false;
            client?.Close();
            client = null;
        }

        void Loop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    var p = Parse(Encoding.UTF8.GetString(client.Receive(ref ep)));
                    if (p == null) continue;
                    lock (gate) latest = p;
                    Interlocked.Exchange(ref lastTicks, DateTime.UtcNow.Ticks);
                }
                catch (SocketException) { if (!running) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { Debug.LogWarning("[let's cook] wrist cam: " + e.Message); }
            }
        }

        static Packet Parse(string json)
        {
            try { return JsonUtility.FromJson<Packet>(json); }
            catch (ArgumentException) { return null; }
        }

        /// For tests: feed a packet as if it came over the network.
        public void Inject(string json)
        {
            latest = Parse(json);
            lastTicks = DateTime.UtcNow.Ticks;
        }

        void Update()
        {
            if (wrists == null) return;
            wrists.externallyDriven = Active;
            if (!Active) return;
            Packet p;
            lock (gate) p = latest;
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            Follow(wrists.left, p.hands?.Left, k);
            Follow(wrists.right, p.hands?.Right, k);
        }

        void Follow(Transform anchor, Hand h, float k)
        {
            if (anchor == null || h == null || !h.ok || h.pos == null || h.pos.Length < 3) return;
            var target = wrists.Clamp(new Vector3(h.pos[0], h.pos[1], h.pos[2]));
            anchor.position = Vector3.Lerp(anchor.position, target, k);
        }
    }
}
