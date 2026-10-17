using System;
using UnityEngine;

namespace LetsCook
{
    /// Asks before recording anything: hand motion can identify a person, so robot-data logging is opt-in.
    /// Y = record (enables the SessionLogger), N = just play. "-consent yes|no" on the command line skips the question.
    public class Consent : MonoBehaviour
    {
        public SessionLogger logger;
        public bool? Answer { get; private set; }

        public const string Text =
            "let's cook! can save your hand movements, the kitchen and two small videos of the counter " +
            "(no camera image of you) to help researchers teach robots to cook. Recordings stay on this computer " +
            "unless the organizers share them as an open dataset. Your name is never saved, and you can ask for " +
            "your recordings to be deleted.";

        void Start()
        {
            if (logger != null) logger.enabled = false;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-consent");
            if (i >= 0 && i + 1 < args.Length) Decide(args[i + 1] == "yes");
        }

        void Update()
        {
            if (Answer.HasValue) return;
            if (Input.GetKeyDown(KeyCode.Y)) Decide(true);
            else if (Input.GetKeyDown(KeyCode.N)) Decide(false);
        }

        public void Decide(bool record)
        {
            Answer = record;
            if (logger != null) logger.enabled = record;
            GameHud.Toast(record ? "recording for robot research. Thank you!" : "playing without recording", 2.5f);
        }

        GUIStyle title, body;

        void OnGUI()
        {
            if (Answer.HasValue) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                title.normal.textColor = new Color(0.25f, 0.12f, 0.05f);
                body = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
                body.normal.textColor = new Color(0.25f, 0.2f, 0.15f);
            }
            float w = Mathf.Min(640f, Screen.width - 40f), h = 280f;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.color = new Color(1f, 0.97f, 0.9f, 0.97f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 16, w, 40), "Before we cook…", title);
            GUI.Label(new Rect(r.x + 28, r.y + 66, w - 56, 140), Text, body);
            GUI.Label(new Rect(r.x, r.y + 214, w, 40), "Y  record and help the robots    ·    N  just play", title);
        }
    }
}
