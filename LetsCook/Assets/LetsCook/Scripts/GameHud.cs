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
        WristCamInput cam;
        SessionLogger logger;

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
            if (cam == null) cam = FindFirstObjectByType<WristCamInput>();
            status += cam != null && cam.Active ? "  ·  wrists: webcam" : "  ·  wrists: keyboard";
            if (logger == null) logger = FindFirstObjectByType<SessionLogger>(FindObjectsInactive.Include);
            status += logger != null && logger.enabled ? "  ·  ● recording" : "  ·  not recording";
            GUI.Label(new Rect(14, 10, 900, 24), "let's cook!  ·  " + status, style);
            GUI.Label(new Rect(14, 32, 1200, 24), Controls, style);
            GUI.Label(new Rect(14, Screen.height - 30, 1200, 24), KitchenLine(), style);

            toasts.RemoveAll(t => t.until < Time.time);
            for (int i = 0; i < toasts.Count; i++)
                GUI.Label(new Rect(0, 70 + i * 30, Screen.width, 30), toasts[i].text, toastStyle);
        }

        Cookware pan;
        Burner burner;
        HandGrasp[] grasps;

        /// Bottom line: burner + pan temperature + oil, and what each hand holds.
        string KitchenLine()
        {
            if (pan == null) pan = FindFirstObjectByType<Cookware>();
            if (burner == null) burner = FindFirstObjectByType<Burner>();
            if (grasps == null || grasps.Length == 0) grasps = FindObjectsByType<HandGrasp>(FindObjectsSortMode.None);
            var parts = new List<string>();
            if (burner != null) parts.Add(burner.on ? $"burner {burner.level * 100:0}%" : "burner off");
            if (pan != null) parts.Add($"pan {pan.temperature:0} °C" + (pan.oil > 0.5f ? $", {pan.oil:0} g oil" : "") + (pan.Smoking ? " (SMOKING)" : ""));
            foreach (var g in grasps)
                if (g != null && g.Held != null) parts.Add($"{g.Hand.side.ToLower()} hand: {g.HeldName}");
            return string.Join("   ·   ", parts);
        }

        public static string Controls = "hands: WASD move · Q/E down/up · Z/C turn · Tab switch hand · Space both  |  B burner · +/- heat  |  T ask for any tool  |  Enter serve the plate  |  K recalibrate gloves  |  R fresh kitchen";
    }
}
