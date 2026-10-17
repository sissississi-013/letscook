using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    /// Minimal on-screen status: glove connection, controls, and short-lived messages from the game.
    public class GameHud : MonoBehaviour
    {
        public RokokoReceiver receiver;
        static readonly List<(string text, float until)> toasts = new List<(string, float)>();
        GUIStyle style, toastStyle;

        public static void Toast(string text, float seconds = 3f)
        {
            toasts.Add((text, Time.time + seconds));
            Debug.Log("[let's cook] " + text);
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 15 };
                style.normal.textColor = new Color(0.2f, 0.15f, 0.1f);
                toastStyle = new GUIStyle(style) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            }
            string status = receiver != null ? receiver.Status : "no receiver";
            GUI.Label(new Rect(14, 10, 900, 24), "let's cook!  ·  " + status, style);
            GUI.Label(new Rect(14, 32, 1200, 24), Controls, style);

            toasts.RemoveAll(t => t.until < Time.time);
            for (int i = 0; i < toasts.Count; i++)
                GUI.Label(new Rect(0, 70 + i * 30, Screen.width, 30), toasts[i].text, toastStyle);
        }

        public static string Controls = "hands: WASD move · Q/E down/up · Z/C turn · Tab switch hand · Space both  |  B burner · +/- heat  |  Enter serve the plate";
    }
}
