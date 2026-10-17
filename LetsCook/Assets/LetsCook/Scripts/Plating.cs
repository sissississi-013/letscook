using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LetsCook
{
    /// Serving a dish: Enter (or Return) collects what's on the plate, sends it to the local chef server
    /// (chef_server.py → Claude), and shows the dish name, course and the unlocked recipe.
    public class Plating : MonoBehaviour
    {
        public Transform plate;
        public Camera shotCamera;
        public string chefUrl = "http://127.0.0.1:8776/dish";
        public float plateRadius = 0.16f;

        [Serializable] public class Unlock { public string recipe_name, course, why, first_step; }
        [Serializable] public class Dish { public string dish_name, course, description, chef_note; public Unlock unlock; public bool offline; public int cookbook_count; }

        public Dish LastDish { get; private set; }
        public bool Busy { get; private set; }
        float shownUntil;
        GUIStyle title, body;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void Update()
        {
            if (ToolRequester.Typing) return;
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && !Busy) Serve();
            if (Input.GetKeyDown(KeyCode.Escape)) shownUntil = 0f;
        }

        public List<Food> FoodOnPlate()
        {
            if (plate == null) return new List<Food>();
            Vector3 c = plate.position;
            return Food.All.Where(f =>
            {
                Vector3 p = f.transform.position;
                return new Vector2(p.x - c.x, p.z - c.z).magnitude < plateRadius && p.y > c.y - 0.01f && p.y < c.y + 0.2f;
            }).ToList();
        }

        public void Serve()
        {
            var onPlate = FoodOnPlate();
            if (onPlate.Count == 0) { GameHud.Toast("put something on the plate first", 2f); return; }
            StartCoroutine(SendDish(SummaryJson(onPlate), Screenshot()));
        }

        /// The dish summary Claude sees: plate contents + what happened while cooking.
        public string SummaryJson(List<Food> onPlate)
        {
            var sb = new StringBuilder("{\"plate\":[");
            for (int i = 0; i < onPlate.Count; i++)
            {
                var f = onPlate[i];
                var s = f.state;
                if (i > 0) sb.Append(',');
                sb.Append("{\"ingredient\":").Append(Q(f.ingredient)).Append(",\"cut\":").Append(Q(f.cut))
                  .Append(",\"doneness\":").Append(Q(s.Doneness))
                  .Append(",\"surface_c\":").Append(Mathf.Round(s.surfaceTemp).ToString(Inv))
                  .Append(",\"core_c\":").Append(Mathf.Round(s.coreTemp).ToString(Inv))
                  .Append(",\"peak_surface_c\":").Append(Mathf.Round(s.peakSurface).ToString(Inv))
                  .Append(",\"browning\":").Append(s.browning.ToString("0.00", Inv))
                  .Append(",\"char\":").Append(s.charred.ToString("0.00", Inv))
                  .Append(",\"seconds_on_heat\":").Append(Mathf.Round(s.secondsHeated).ToString(Inv))
                  .Append(",\"salt_g\":").Append(s.salt.ToString("0.00", Inv))
                  .Append(",\"pepper_g\":").Append(s.pepper.ToString("0.00", Inv))
                  .Append(",\"seasoning\":").Append(Q(s.Seasoning)).Append('}');
            }
            sb.Append("],\"events\":{");
            var groups = CookEvents.History.GroupBy(e => e.type + ":" + e.subject).OrderBy(g => g.Key).ToList();
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Q(groups[i].Key)).Append(':').Append(groups[i].Count());
            }
            float minutes = CookEvents.History.Count > 0 ? (Time.time - CookEvents.History[0].time) / 60f : 0f;
            sb.Append("},\"minutes_cooking\":").Append(minutes.ToString("0.0", Inv));
            sb.Append(",\"left_in_kitchen\":[");
            var rest = Food.All.Except(onPlate).Select(f => Q(f.ingredient + " (" + f.cut + ", " + f.state.Doneness + ")")).ToList();
            sb.Append(string.Join(",", rest)).Append("]}");
            return sb.ToString();
        }

        string Screenshot()
        {
            var cam = shotCamera != null ? shotCamera : Camera.main;
            if (cam == null) return null;
            var rt = new RenderTexture(640, 360, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 360, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            tex.Apply();
            cam.targetTexture = prev;
            RenderTexture.active = null;
            rt.Release();
            return Convert.ToBase64String(tex.EncodeToPNG());
        }

        IEnumerator SendDish(string summary, string screenshot)
        {
            Busy = true;
            GameHud.Toast("the chef is tasting…", 2.5f);
            string body = "{\"summary\":" + summary + (screenshot != null ? ",\"screenshot_b64\":" + Q(screenshot) : "") + "}";
            using (var req = new UnityWebRequest(chefUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 60;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                    LastDish = JsonUtility.FromJson<Dish>(req.downloadHandler.text);
                else
                {
                    Debug.LogWarning("[let's cook] chef server unreachable: " + req.error);
                    LastDish = new Dish
                    {
                        dish_name = "Untitled Masterpiece", course = "mystery", offline = true,
                        description = "The chef server isn't running (start chef_server.py).",
                        chef_note = "Your plate was still logged.",
                        unlock = new Unlock { recipe_name = "anything", course = "any", why = "free play", first_step = "keep cooking" },
                    };
                }
            }
            CookEvents.Emit("plate", LastDish.dish_name, LastDish.course);
            shownUntil = Time.time + 12f;
            Busy = false;
        }

        void OnGUI()
        {
            if (LastDish == null || Time.time > shownUntil) return;
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                title.normal.textColor = new Color(0.25f, 0.12f, 0.05f);
                body = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperCenter, wordWrap = true };
                body.normal.textColor = new Color(0.25f, 0.2f, 0.15f);
            }
            float w = Mathf.Min(620f, Screen.width - 40f), h = 300f;
            var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.color = new Color(1f, 0.97f, 0.9f, 0.96f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 20, r.y + 14, w - 40, 70), LastDish.dish_name, title);
            GUI.Label(new Rect(r.x + 20, r.y + 84, w - 40, 24), LastDish.course.ToUpperInvariant() + (LastDish.offline ? " · offline" : ""), body);
            GUI.Label(new Rect(r.x + 20, r.y + 112, w - 40, 60), LastDish.description, body);
            GUI.Label(new Rect(r.x + 20, r.y + 172, w - 40, 40), LastDish.chef_note, body);
            if (LastDish.unlock != null)
                GUI.Label(new Rect(r.x + 20, r.y + 218, w - 40, 70),
                    $"Unlocked: {LastDish.unlock.recipe_name} ({LastDish.unlock.course})\n{LastDish.unlock.first_step}", body);
        }

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
