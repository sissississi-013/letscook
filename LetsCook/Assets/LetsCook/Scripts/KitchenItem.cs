using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    public enum ItemKind { Tool, Ingredient, Container, Fixture }

    /// Anything in the kitchen the game (and the robot-data logger) should know by name.
    public class KitchenItem : MonoBehaviour
    {
        public string displayName;
        public ItemKind kind;
        public static readonly List<KitchenItem> All = new List<KitchenItem>();
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}
