using UnityEngine;

namespace LetsCook
{
    /// R = fresh kitchen (new ingredients, tools back in place) for the next dish or the next player.
    /// Emits a "reset" event, which also ends the current robot-data episode.
    public class KitchenReset : MonoBehaviour
    {
        void Update()
        {
            if (ToolRequester.Typing) return;
            if (Input.GetKeyDown(KeyCode.R)) ResetKitchen();
        }

        public Transform ResetKitchen()
        {
            foreach (var grasp in FindObjectsByType<HandGrasp>(FindObjectsSortMode.None)) grasp.Release("kitchen reset");
            var old = GameObject.Find("Kitchen");
            Transform parent = old != null ? old.transform.parent : null;
            if (old != null) { old.name = "Kitchen (old)"; Destroy(old); }
            foreach (var tool in FindObjectsByType<ToolInfo>(FindObjectsSortMode.None)) Destroy(tool.gameObject);   // requested tools
            foreach (var food in Food.All.ToArray()) if (food != null) Destroy(food.gameObject);                      // spilled eggs, pieces
            foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name == "Eggshell" && go.transform.parent == parent) Destroy(go);

            var kitchen = Kitchen.Build(parent);
            var plating = FindFirstObjectByType<Plating>();
            if (plating != null) plating.plate = kitchen.Find("Plate");
            CookEvents.Emit("reset", "kitchen");
            GameHud.Toast("fresh kitchen!", 1.5f);
            return kitchen;
        }
    }
}
