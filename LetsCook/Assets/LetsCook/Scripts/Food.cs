using UnityEngine;

namespace LetsCook
{
    /// Continuous cooking state of one piece of food. Filled in by M5.
    public class Food : MonoBehaviour
    {
        public string ingredient;

        public void Init(string name)
        {
            ingredient = name;
        }
    }
}
