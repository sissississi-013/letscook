using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LetsCook.Tests
{
    public class ScriptFileTests
    {
        /// A MonoBehaviour must live in a file with its own name, or scenes silently lose it ("missing script").
        [Test]
        public void EveryComponentHasItsOwnScriptFile()
        {
            var types = typeof(GloveHand).Assembly.GetTypes().Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract);
            var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/LetsCook/Scripts" })
                .Select(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)))
                .Select(s => s.GetClass()).Where(c => c != null).ToList();
            foreach (var t in types)
                Assert.Contains(t, scripts, $"{t.Name} needs to be in {t.Name}.cs");
        }
    }
}
