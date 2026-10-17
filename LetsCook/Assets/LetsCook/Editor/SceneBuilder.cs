using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LetsCook.EditorTools
{
    /// Builds the Kitchen scene from code and captures screenshots in batch mode.
    ///   Unity -batchmode -quit -projectPath LetsCook -executeMethod LetsCook.EditorTools.SceneBuilder.Build
    ///   Unity -batchmode -quit -projectPath LetsCook -executeMethod LetsCook.EditorTools.SceneBuilder.Shot -shotPose pinch -shotName m2
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Kitchen.unity";
        public static readonly Vector3 LeftWrist = new Vector3(-0.22f, 1.0f, 0.35f);
        public static readonly Vector3 RightWrist = new Vector3(0.22f, 1.0f, 0.35f);

        [MenuItem("Let's Cook/Rebuild Kitchen Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.97f, 0.93f, 0.86f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.02f;
            cam.fieldOfView = 55f;
            camGo.transform.position = new Vector3(0f, 1.5f, -0.25f);
            camGo.transform.LookAt(new Vector3(-0.02f, 0.88f, 0.5f));
            camGo.AddComponent<AudioListener>();

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.95f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.52f, 0.5f);

            var rig = new GameObject("Glove Rig");
            var receiver = rig.AddComponent<RokokoReceiver>();
            var wrists = rig.AddComponent<WristController>();
            wrists.left = MakeHand(rig.transform, receiver, "Left", LeftWrist);
            wrists.right = MakeHand(rig.transform, receiver, "Right", RightWrist);

            Kitchen.Build();
            rig.AddComponent<GameHud>().receiver = receiver;
            KitchenHook?.Invoke(scene);
            SaveMaterials();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[let's cook] Built " + ScenePath);
        }

        /// Materials made in code must become assets, or the saved scene loses them.
        static void SaveMaterials()
        {
            const string dir = "Assets/LetsCook/Materials";
            Directory.CreateDirectory(dir);
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m == null || AssetDatabase.Contains(m)) continue;
                string path = $"{dir}/{m.name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null) { EditorUtility.CopySerialized(m, existing); r.sharedMaterial = existing; }
                else AssetDatabase.CreateAsset(m, path);
            }
            AssetDatabase.SaveAssets();
        }

        /// Later milestones can add more to the scene here.
        public static Action<Scene> KitchenHook;

        static Transform MakeHand(Transform parent, RokokoReceiver receiver, string side, Vector3 wrist)
        {
            var anchor = new GameObject(side + "WristAnchor").transform;
            anchor.SetParent(parent, false);
            anchor.position = wrist;
            var hand = new GameObject(side + " Hand").AddComponent<GloveHand>();
            hand.transform.SetParent(parent, false);
            hand.side = side;
            hand.receiver = receiver;
            hand.anchor = anchor;
            hand.gameObject.AddComponent<HandGrasp>();
            return anchor;
        }

        public static void Shot()
        {
            string pose = Arg("-shotPose", "pinch");
            string name = Arg("-shotName", "shot");
            EditorSceneManager.OpenScene(ScenePath);

            var receiver = UnityEngine.Object.FindFirstObjectByType<RokokoReceiver>();
            var asset = Resources.Load<TextAsset>("MockFrames/mock_" + pose);
            var frame = GloveFrame.Parse(asset.text);
            receiver.Inject(frame);
            foreach (var hand in UnityEngine.Object.FindObjectsByType<GloveHand>(FindObjectsSortMode.None))
                hand.Apply(frame);
            ShotHook?.Invoke();

            Capture(Camera.main, name);
        }

        /// Later milestones can stage things (e.g. cook food) before the capture.
        public static Action ShotHook;

        public static void Capture(Camera cam, string name, int width = 1280, int height = 720)
        {
            var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../shots"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[let's cook] Screenshot " + path);
        }

        static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
    }
}
