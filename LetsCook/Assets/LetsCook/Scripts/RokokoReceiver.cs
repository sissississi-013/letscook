using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LetsCook
{
    /// Receives Rokoko Studio Custom Streaming (JSON v4, uncompressed) on a background thread.
    /// When nothing arrives for a while, plays the built-in mock animation so the game still runs.
    public class RokokoReceiver : MonoBehaviour
    {
        public int port = 14045;
        public bool mockWhenSilent = true;
        public float silentSeconds = 2f;

        UdpClient client;
        Thread thread;
        volatile bool running;
        readonly object gate = new object();
        GloveFrame latest;
        long lastPacketTicks;
        int packets;
        bool warnedLz4;

        GloveFrame[] mockFrames;
        static readonly string[] MockCycle = { "open", "fist", "open", "pinch", "open", "point" };
        GloveFrame injected;

        public int PacketsReceived => packets;
        public bool IsLive => latest != null && (DateTime.UtcNow.Ticks - Interlocked.Read(ref lastPacketTicks)) < TimeSpan.FromSeconds(silentSeconds).Ticks;

        /// Latest frame: live data if streaming, else an injected frame, else the mock animation.
        public GloveFrame Latest
        {
            get
            {
                if (IsLive) lock (gate) return latest;
                if (injected != null) return injected;
                return mockWhenSilent ? MockFrame(Application.isPlaying ? Time.time : 0f) : null;
            }
        }

        public string Status => IsLive ? $"LIVE gloves ({latest.source}) · {packets} packets" : (injected != null ? "injected frame" : "mock hands (no glove stream on :" + port + ")");

        /// Only real (or injected) glove data moves things. The idle mock animation is a ghost, so it can't
        /// knock the kitchen over while nobody is wearing the gloves.
        public bool DrivesPhysics => IsLive || injected != null;

        /// For tests and screenshots: force a frame regardless of the network.
        public void Inject(GloveFrame frame) => injected = frame;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            try
            {
                client = new UdpClient(port);
                client.Client.ReceiveBufferSize = 1 << 20;
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[let's cook] Can't listen on UDP {port}: {e.Message}");
                return;
            }
            running = true;
            thread = new Thread(Loop) { IsBackground = true, Name = "RokokoReceiver" };
            thread.Start();
            Debug.Log($"[let's cook] Listening for Rokoko JSON v4 on UDP {port}");
        }

        void OnDisable()
        {
            running = false;
            client?.Close();
            client = null;
        }

        void Loop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = client.Receive(ref endpoint);
                    if (data.Length >= 4 && data[0] == 0x04 && data[1] == 0x22 && data[2] == 0x4D && data[3] == 0x18)
                    {
                        if (!warnedLz4) { warnedLz4 = true; Debug.LogWarning("[let's cook] Got LZ4 data. Set the Studio receiver for port " + port + " to plain \"JSON v4\"."); }
                        continue;
                    }
                    var frame = GloveFrame.Parse(Encoding.UTF8.GetString(data));
                    if (frame == null) continue;
                    lock (gate) latest = frame;
                    Interlocked.Exchange(ref lastPacketTicks, DateTime.UtcNow.Ticks);
                    Interlocked.Increment(ref packets);
                }
                catch (SocketException) { if (!running) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { Debug.LogWarning("[let's cook] receiver: " + e.Message); }
            }
        }

        public GloveFrame MockFrame(float time)
        {
            if (mockFrames == null)
            {
                mockFrames = new GloveFrame[MockCycle.Length];
                for (int i = 0; i < MockCycle.Length; i++)
                {
                    var asset = Resources.Load<TextAsset>("MockFrames/mock_" + MockCycle[i]);
                    mockFrames[i] = asset != null ? GloveFrame.Parse(asset.text) : null;
                }
            }
            const float secondsPerPose = 1.5f;
            float phase = time / secondsPerPose;
            int i0 = (int)phase % MockCycle.Length;
            var a = mockFrames[i0];
            var b = mockFrames[(i0 + 1) % MockCycle.Length];
            if (a == null || b == null) return null;
            float k = phase - Mathf.Floor(phase);
            return GloveFrame.Lerp(a, b, k * k * (3 - 2 * k));
        }
    }
}
