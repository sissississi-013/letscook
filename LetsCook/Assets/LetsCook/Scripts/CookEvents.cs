using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    /// Everything that happens in the kitchen, in order: grab, release, cut, plate...
    /// Feeds the session logger (robot labels) and the dish summary sent to Claude.
    public static class CookEvents
    {
        [Serializable]
        public struct Entry
        {
            public float time;
            public string type;
            public string subject;
            public string detail;
        }

        public static readonly List<Entry> History = new List<Entry>();
        public static event Action<Entry> Emitted;

        public static void Emit(string type, string subject, string detail = "")
        {
            var e = new Entry { time = Application.isPlaying ? Time.time : 0f, type = type, subject = subject, detail = detail };
            History.Add(e);
            if (History.Count > 2000) History.RemoveAt(0);
            Emitted?.Invoke(e);
        }

        public static void Clear() => History.Clear();
    }
}
