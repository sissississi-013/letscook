using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LetsCook
{
    /// Press T, type any tool ("a mortar and pestle"), Enter: the chef server returns a part list
    /// (library first, else Claude) and the tool drops onto the counter.
    public class ToolRequester : MonoBehaviour
    {
        public string chefUrl = "http://127.0.0.1:8776/tool";
        public Vector3 spawnPoint = new Vector3(0f, 1.05f, 0.25f);
        public static bool Typing { get; private set; }

        string text = "";
        bool busy;

        void Update()
        {
            if (!Typing && !busy && Input.GetKeyDown(KeyCode.T)) { Typing = true; text = ""; }
        }

        void OnGUI()
        {
            if (!Typing) return;
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                Typing = false;
                if (text.Trim().Length > 0) StartCoroutine(Request(text.Trim()));
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Typing = false; e.Use(); return; }

            float w = Mathf.Min(560f, Screen.width - 40f);
            var r = new Rect((Screen.width - w) / 2f, Screen.height - 110f, w, 70f);
            GUI.Box(r, "what tool do you need? (Enter to ask · Esc to cancel)");
            GUI.SetNextControlName("toolText");
            text = GUI.TextField(new Rect(r.x + 14, r.y + 30, w - 28, 28), text, 120);
            GUI.FocusControl("toolText");
        }

        public IEnumerator Request(string request)
        {
            busy = true;
            GameHud.Toast($"finding a {request}…", 2f);
            string body = "{\"request\":\"" + request.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            using (var req = new UnityWebRequest(chefUrl, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 60;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    GameHud.Toast("the chef server isn't running (start chef_server.py)", 3f);
                    busy = false;
                    yield break;
                }
                var res = JsonUtility.FromJson<ToolResponse>(req.downloadHandler.text);
                if (res?.tool?.parts != null)
                {
                    ToolBuilder.Build(res.tool, spawnPoint, res.source);
                    GameHud.Toast($"here's your {res.tool.name} ({res.source})", 2.5f);
                }
            }
            busy = false;
        }
    }
}
