using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LetsCook.EditorTools
{
    /// Turns a take recorded in Rokoko Studio (free plan: record → export FBX) into glove frames the game and the
    /// robot-data tools understand: sessions/take-<name>.jsonl in receiver.py's format ({"t":..., "frame": JSON v4}).
    /// Replay it into the game with `python3 replay_take.py sessions/take-<name>.jsonl`.
    ///   Unity -batchmode -quit -projectPath LetsCook -executeMethod LetsCook.EditorTools.TakeImporter.Import -fbx /path/take.fbx
    public static class TakeImporter
    {
        static readonly string[] Parts = { "Metacarpal", "Proximal", "Medial", "Distal", "Tip" };
        // Other common finger naming schemes (Mixamo / Unity humanoid style) → Rokoko JSON v4 suffix.
        static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "HandThumb1", "Finger1Metacarpal" }, { "HandThumb2", "Finger1Proximal" }, { "HandThumb3", "Finger1Distal" }, { "HandThumb4", "Finger1Tip" },
            { "HandIndex1", "Finger2Proximal" }, { "HandIndex2", "Finger2Medial" }, { "HandIndex3", "Finger2Distal" }, { "HandIndex4", "Finger2Tip" },
            { "HandMiddle1", "Finger3Proximal" }, { "HandMiddle2", "Finger3Medial" }, { "HandMiddle3", "Finger3Distal" }, { "HandMiddle4", "Finger3Tip" },
            { "HandRing1", "Finger4Proximal" }, { "HandRing2", "Finger4Medial" }, { "HandRing3", "Finger4Distal" }, { "HandRing4", "Finger4Tip" },
            { "HandPinky1", "Finger5Proximal" }, { "HandPinky2", "Finger5Medial" }, { "HandPinky3", "Finger5Distal" }, { "HandPinky4", "Finger5Tip" },
        };

        [MenuItem("Let's Cook/Import Rokoko FBX Take…")]
        static void ImportMenu()
        {
            string path = EditorUtility.OpenFilePanel("Rokoko take (FBX)", "", "fbx");
            if (!string.IsNullOrEmpty(path)) Debug.Log("[let's cook] " + ImportFile(path));
        }

        public static void Import()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-fbx");
            if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("pass -fbx <path>");
            Debug.Log("[let's cook] " + ImportFile(args[i + 1]));
        }

        /// Canonical Rokoko joint name for a bone ("Rokoko:LeftFinger2Proximal", "mixamorig:LeftHandIndex1" → "LeftFinger2Proximal").
        public static string Canonical(string bone)
        {
            string n = bone.Contains(":") ? bone.Substring(bone.LastIndexOf(':') + 1) : bone;
            n = n.Replace("_", "").Replace(" ", "");
            foreach (var side in new[] { "Left", "Right" })
            {
                if (!n.StartsWith(side, StringComparison.OrdinalIgnoreCase)) continue;
                string rest = n.Substring(side.Length);
                if (rest.Equals("Hand", StringComparison.OrdinalIgnoreCase)) return side + "Hand";
                if (rest.StartsWith("Finger") && Parts.Any(p => rest.EndsWith(p))) return side + rest;
                foreach (var kv in Aliases) if (rest.Equals(kv.Key, StringComparison.OrdinalIgnoreCase)) return side + kv.Value;
            }
            return null;
        }

        public static string ImportFile(string fbxPath)
        {
            string name = Path.GetFileNameWithoutExtension(fbxPath).Replace(" ", "_");
            const string dir = "Assets/LetsCook/Takes";
            Directory.CreateDirectory(dir);
            string assetPath = $"{dir}/{name}.fbx";
            File.Copy(fbxPath, assetPath, true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var clip = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (model == null || clip == null) return $"no model/animation in {fbxPath}";

            var go = UnityEngine.Object.Instantiate(model);
            try
            {
                var bones = new Dictionary<string, Transform>();
                foreach (var t in go.GetComponentsInChildren<Transform>())
                {
                    var c = Canonical(t.name);
                    if (c != null && !bones.ContainsKey(c)) bones[c] = t;
                }
                if (!bones.ContainsKey("LeftHand") && !bones.ContainsKey("RightHand"))
                    return $"no hand bones found in {fbxPath} (bones: {string.Join(", ", go.GetComponentsInChildren<Transform>().Take(20).Select(t => t.name))}…)";

                var order = bones.Keys.OrderBy(k => k).ToList();
                var index = order.Select((k, i2) => (k, i2)).ToDictionary(p => p.k, p => p.i2);
                string Parent(string joint)
                {
                    var p = bones[joint].parent;
                    while (p != null) { var c = Canonical(p.name); if (c != null && index.ContainsKey(c)) return c; p = p.parent; }
                    return null;
                }
                var parents = order.ToDictionary(k => k, Parent);

                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../sessions"));
                Directory.CreateDirectory(outDir);
                string outPath = Path.Combine(outDir, $"take-{name}.jsonl");
                float fps = clip.frameRate > 0 ? clip.frameRate : 30f;
                int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * fps));
                var inv = CultureInfo.InvariantCulture;
                using (var w = new StreamWriter(outPath, false, new UTF8Encoding(false)))
                {
                    for (int f = 0; f <= frames; f++)
                    {
                        float time = f / fps;
                        clip.SampleAnimation(go, time);
                        var sb = new StringBuilder();
                        sb.Append("{\"t\":").Append(time.ToString("0.####", inv)).Append(",\"frame\":{\"version\":\"3,0\",\"fps\":").Append(fps.ToString(inv))
                          .Append(",\"scene\":{\"timestamp\":").Append(time.ToString("0.####", inv))
                          .Append(",\"actors\":[],\"props\":[],\"characters\":[],\"newtons\":[{\"name\":\"Take ").Append(name)
                          .Append("\",\"meta\":{\"hasHands\":true,\"hasBody\":false},\"joints\":[");
                        for (int j = 0; j < order.Count; j++)
                        {
                            var t = bones[order[j]];
                            var p = t.position; var q = t.rotation;
                            int parent = parents[order[j]] != null ? index[parents[order[j]]] : -1;
                            if (j > 0) sb.Append(',');
                            sb.Append("{\"name\":\"").Append(order[j]).Append("\",\"parent\":").Append(parent)
                              .Append(",\"position\":{\"x\":").Append(p.x.ToString("0.#####", inv)).Append(",\"y\":").Append(p.y.ToString("0.#####", inv)).Append(",\"z\":").Append(p.z.ToString("0.#####", inv))
                              .Append("},\"rotation\":{\"x\":").Append(q.x.ToString("0.#####", inv)).Append(",\"y\":").Append(q.y.ToString("0.#####", inv)).Append(",\"z\":").Append(q.z.ToString("0.#####", inv)).Append(",\"w\":").Append(q.w.ToString("0.#####", inv)).Append("}}");
                        }
                        sb.Append("]}]}}}");
                        w.WriteLine(sb.ToString());
                    }
                }
                return $"take '{name}': {frames + 1} frames at {fps} fps, {order.Count} hand joints → {outPath}";
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
