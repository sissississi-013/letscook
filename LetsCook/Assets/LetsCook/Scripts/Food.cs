using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    /// A piece of food in the world: runs FoodState, finds hot surfaces it touches, and recolours itself.
    public class Food : MonoBehaviour
    {
        public string ingredient;
        public string cut = "whole";   // whole, half, slice, piece...
        public FoodState state;
        [System.NonSerialized] public float cutCooldownUntil;
        public static readonly List<Food> All = new List<Food>();
        public static float TimeScale = 1f;   // 1 = real time; raise for faster demos

        Renderer rend;
        Material mat;
        Vector3 baseScale;
        readonly HashSet<Cookware> touching = new HashSet<Cookware>();

        public void Init(string name)
        {
            ingredient = name;
            state = new FoodState(IngredientProfile.For(name));
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (state == null) Init(ingredient);
            rend = GetComponent<Renderer>();
            mat = null;   // a cut copy must not share its parent's material instance
            baseScale = transform.localScale;
            // Sleeping bodies stop reporting contacts, which would silently stop the cooking.
            if (TryGetComponent<Rigidbody>(out var rb)) rb.sleepThreshold = 0f;
        }

        void OnCollisionStay(Collision c)
        {
            var pan = c.rigidbody != null ? c.rigidbody.GetComponent<Cookware>() : c.collider.GetComponentInParent<Cookware>();
            if (pan != null) touching.Add(pan);
        }

        void FixedUpdate()
        {
            float? contact = null;
            float boost = 1f;
            foreach (var pan in touching)
                if (pan != null)
                {
                    contact = Mathf.Max(contact ?? float.MinValue, pan.temperature);
                    boost = Mathf.Max(boost, pan.ContactBoost);
                    if (pan.oil > 1f && pan.temperature > 60f && state.oil < 2f)
                        state.oil += 0.02f * Time.fixedDeltaTime * TimeScale;   // food soaks up a little oil
                }
            touching.Clear();
            state.Step(Time.fixedDeltaTime * TimeScale, contact, boost);
        }

        void Update() => ApplyLook();

        public void ApplyLook()
        {
            if (rend == null) rend = GetComponent<Renderer>();
            if (rend == null) return;
            if (mat == null || rend.sharedMaterials[0] != mat)
            {
                mat = new Material(rend.sharedMaterials[0]);
                var mats = rend.sharedMaterials;   // keep extra slots (e.g. the flesh of a cut face)
                mats[0] = mat;
                rend.sharedMaterials = mats;
            }
            mat.color = state.Color;
            if (state.profile.meltTemp > 0f && baseScale != Vector3.zero)
                transform.localScale = new Vector3(baseScale.x * (1f + state.melted * 0.6f), baseScale.y * (1f - state.melted * 0.75f), baseScale.z * (1f + state.melted * 0.6f));
        }

        public void Season(string seasoning, float grams)
        {
            if (seasoning == "pepper") state.pepper += grams;
            else if (seasoning == "oil") state.oil += grams;
            else state.salt += grams;
        }

        /// One line for the dish summary / robot labels.
        public string Describe() =>
            $"{ingredient} ({cut}): {state.Doneness}, surface {state.surfaceTemp:0}°C, core {state.coreTemp:0}°C, browning {state.browning:0.00}, char {state.charred:0.00}{(state.Seasoning.Length > 0 ? ", " + state.Seasoning : "")}";
    }
}
