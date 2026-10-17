using System;
using UnityEngine;

namespace LetsCook
{
    /// What a built tool can do (for logging, robot labels and future behaviours).
    public class ToolInfo : MonoBehaviour
    {
        public string source;
        public string[] abilities = new string[0];
        public bool Can(string ability) => Array.IndexOf(abilities, ability) >= 0;
    }
}
