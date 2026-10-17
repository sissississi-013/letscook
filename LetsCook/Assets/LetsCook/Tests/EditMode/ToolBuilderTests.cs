using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LetsCook.Tests
{
    public class ToolBuilderTests
    {
        [System.Serializable] class ToolList { public ToolSpec[] items; }

        static ToolSpec[] Library()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "../../tools_library.json"));
            return JsonUtility.FromJson<ToolList>("{\"items\":" + json + "}").items;
        }

        [Test]
        public void EveryLibraryToolBuildsAsOnePhysicalItem()
        {
            foreach (var spec in Library())
            {
                var go = ToolBuilder.Build(spec, Vector3.up);
                try
                {
                    Assert.AreEqual(spec.mass_kg, go.GetComponent<Rigidbody>().mass, 1e-4, spec.name);
                    Assert.Greater(go.GetComponentsInChildren<Collider>().Length, 0, spec.name);
                    Assert.AreEqual(ItemKind.Tool, go.GetComponent<KitchenItem>().kind);
                    Assert.AreEqual(1, go.GetComponentsInChildren<Rigidbody>().Length, spec.name + " must be one rigid body");
                    var renderers = go.GetComponentsInChildren<Renderer>();
                    var b = renderers[0].bounds;
                    foreach (var r in renderers) b.Encapsulate(r.bounds);
                    Assert.Less(b.size.magnitude, 1.0f, spec.name + " should be kitchen-sized");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void OnlyCuttingToolsGetABladeEdge()
        {
            var lib = Library();
            var cleaver = ToolBuilder.Build(lib.First(t => t.name == "cleaver"), Vector3.zero);
            var whisk = ToolBuilder.Build(lib.First(t => t.name == "whisk"), Vector3.zero);
            var peeler = ToolBuilder.Build(lib.First(t => t.name == "peeler"), Vector3.zero);
            try
            {
                var edge = cleaver.GetComponentInChildren<KnifeEdge>();
                Assert.NotNull(edge);
                Assert.That(Mathf.Abs(Vector3.Dot(edge.BladeNormal, Vector3.forward)), Is.GreaterThan(0.99f), "cleaver blade is thin along z");
                Assert.IsNull(whisk.GetComponentInChildren<KnifeEdge>());
                var peelerEdge = peeler.GetComponentInChildren<KnifeEdge>();
                Assert.That(Mathf.Abs(Vector3.Dot(peelerEdge.BladeNormal, Vector3.up)), Is.GreaterThan(0.99f), "peeler blade is thin along y");
            }
            finally { Object.DestroyImmediate(cleaver); Object.DestroyImmediate(whisk); Object.DestroyImmediate(peeler); }
        }

        [Test]
        public void ParsesAServerResponse()
        {
            var res = JsonUtility.FromJson<ToolResponse>("{\"source\":\"claude\",\"tool\":{\"name\":\"spiralizer\",\"mass_kg\":0.4,\"abilities\":[\"cuts\"],\"parts\":[{\"role\":\"blade\",\"shape\":\"box\",\"size\":[0.1,0.004,0.1],\"offset\":[0,0,0],\"rotation\":[0,0,0],\"material\":\"steel\"}]}}");
            Assert.AreEqual("claude", res.source);
            var go = ToolBuilder.Build(res.tool, Vector3.zero, res.source);
            Assert.AreEqual("claude", go.GetComponent<ToolInfo>().source);
            Assert.IsTrue(go.GetComponent<ToolInfo>().Can("cuts"));
            Object.DestroyImmediate(go);
        }
    }
}
