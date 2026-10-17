using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LetsCook
{
    /// Records robot-learning video next to the session log: an egocentric view (the player's camera) and a fixed
    /// third-person "agentview". SessionLogger calls Capture() once per logged frame, so frame N of each video
    /// matches the log line with "video_frame": N. Frames are piped raw into ffmpeg (hardware H.264).
    public class SessionVideo : MonoBehaviour
    {
        public Camera ego;
        public Camera agent;
        public int width = 424, height = 240;
        public float fps = 30f;

        public string Directory { get; private set; }
        public int Frames { get; private set; }
        public bool Recording => streams != null;

        Stream[] streams;
        RenderTexture rt;
        Texture2D tex;

        class Stream
        {
            public Camera cam;
            public Process ffmpeg;
            public Thread writer;
            public BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>(120);
        }

        public static string FindFfmpeg()
        {
            foreach (var p in new[] { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg" })
                if (File.Exists(p)) return p;
            return null;
        }

        /// Starts both videos in `dir`. Returns false (and records nothing) when ffmpeg isn't installed.
        public bool Begin(string dir)
        {
            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null) { Debug.LogWarning("[let's cook] ffmpeg not found: no session video"); return false; }
            System.IO.Directory.CreateDirectory(dir);
            Directory = dir;
            rt = new RenderTexture(width, height, 24);
            tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var cams = new[] { ("ego", ego), ("agentview", agent) };
            streams = new Stream[cams.Length];
            for (int i = 0; i < cams.Length; i++)
            {
                var s = new Stream { cam = cams[i].Item2 };
                string output = Path.Combine(dir, cams[i].Item1 + ".mp4");
                s.ffmpeg = Process.Start(new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    Arguments = $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgb24 -s {width}x{height} -r {fps} -i - " +
                                $"-vf vflip -c:v h264_videotoolbox -b:v 2M -pix_fmt yuv420p \"{output}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                });
                var proc = s.ffmpeg;
                var queue = s.queue;
                s.writer = new Thread(() =>
                {
                    var stdin = proc.StandardInput.BaseStream;
                    try { foreach (var frame in queue.GetConsumingEnumerable()) stdin.Write(frame, 0, frame.Length); }
                    catch (IOException e) { Debug.LogWarning("[let's cook] video writer: " + e.Message); }
                    finally { try { stdin.Close(); } catch (IOException) { } }
                }) { IsBackground = true, Name = "SessionVideo" };
                s.writer.Start();
                streams[i] = s;
            }
            return true;
        }

        /// Renders one frame of each view. Returns the frame index, or -1 if not recording.
        public int Capture()
        {
            if (streams == null) return -1;
            foreach (var s in streams)
            {
                byte[] bytes;
                if (s.cam != null)
                {
                    var prev = s.cam.targetTexture;
                    s.cam.targetTexture = rt;
                    s.cam.Render();
                    s.cam.targetTexture = prev;
                    var active = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    RenderTexture.active = active;
                    bytes = tex.GetRawTextureData();
                }
                else bytes = new byte[width * height * 3];
                if (!s.queue.TryAdd(bytes, 200)) Debug.LogWarning("[let's cook] video queue full, frame dropped");
            }
            return Frames++;
        }

        public void End()
        {
            if (streams == null) return;
            foreach (var s in streams) s.queue.CompleteAdding();
            foreach (var s in streams)
            {
                s.writer.Join(5000);
                if (!s.ffmpeg.WaitForExit(10000)) s.ffmpeg.Kill();
            }
            streams = null;
            if (rt != null) rt.Release();
            Debug.Log($"[let's cook] session video: {Frames} frames → {Directory}");
        }

        void OnDisable() => End();
    }
}
