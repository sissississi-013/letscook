using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LetsCook
{
    /// Talks to Rokoko Studio's Command API (Studio → Settings → Command API; defaults port 14053, key 1234).
    /// K = recalibrate the gloves (3 s countdown: hold your hands in the calibration pose).
    public class StudioCommands : MonoBehaviour
    {
        public string baseUrl = "http://127.0.0.1:14053/v1/1234";
        public bool Busy { get; private set; }
        public string LastResponse { get; private set; }

        void Update()
        {
            if (ToolRequester.Typing || Busy) return;
            if (Input.GetKeyDown(KeyCode.K)) StartCoroutine(Calibrate());
        }

        public IEnumerator Calibrate()
        {
            GameHud.Toast("calibrating gloves in 3… hold the calibration pose", 3.5f);
            yield return Post("calibrate", "{\"device_id\":\"\",\"countdown_delay\":3,\"skip_suit\":true,\"skip_gloves\":false,\"use_custom_pose\":false,\"pose\":\"straight-arms-down\"}");
            if (LastResponse != null && LastResponse.Contains("\"response_code\":0")) CookEvents.Emit("calibrate", "gloves", "studio");
        }

        public IEnumerator Post(string command, string json)
        {
            Busy = true;
            using (var req = new UnityWebRequest($"{baseUrl}/{command}", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                LastResponse = req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null;
                if (LastResponse == null)
                    GameHud.Toast("can't reach Rokoko Studio: turn on Settings → Command API", 3f);
                else if (!LastResponse.Contains("\"response_code\":0"))
                    GameHud.Toast("Studio says: " + Describe(LastResponse), 3f);
                Debug.Log($"[let's cook] studio {command}: {LastResponse ?? req.error}");
            }
            Busy = false;
        }

        static string Describe(string response)
        {
            int i = response.IndexOf("\"description\":\"");
            if (i < 0) return response;
            i += 15;
            int j = response.IndexOf('"', i);
            return j > i ? response.Substring(i, j - i) : response;
        }
    }
}
