using System;
using UnityEngine;

namespace LetsCook
{
    /// Per-ingredient physical constants. Rough but grounded: water boils off before browning starts,
    /// Maillard browning from about 140 °C, charring above about 180 °C, egg white sets at about 62 °C.
    [Serializable]
    public class IngredientProfile
    {
        public string name;
        public float startTemp = 20f;      // °C
        public float water = 0.8f;         // surface water fraction (must boil off before browning)
        public float coreRate = 0.02f;     // 1/s, how fast the core follows the surface (smaller = thicker)
        public float setTemp = 0f;         // protein set temperature (0 = none)
        public float meltTemp = 0f;        // fat melt temperature (0 = none)
        public Color raw = Color.white;
        public Color cooked = Color.white; // fully set / softened colour before browning

        public static IngredientProfile For(string ingredient)
        {
            switch (ingredient)
            {
                case "tomato": return new IngredientProfile { name = ingredient, startTemp = 18f, water = 0.95f, coreRate = 0.012f, raw = new Color(0.86f, 0.16f, 0.1f), cooked = new Color(0.72f, 0.12f, 0.06f) };
                case "onion": return new IngredientProfile { name = ingredient, startTemp = 18f, water = 0.9f, coreRate = 0.012f, setTemp = 85f, raw = new Color(0.82f, 0.62f, 0.34f), cooked = new Color(0.9f, 0.78f, 0.55f) };
                case "egg": return new IngredientProfile { name = ingredient, startTemp = 5f, water = 0.88f, coreRate = 0.015f, setTemp = 62f, raw = new Color(0.98f, 0.93f, 0.84f), cooked = new Color(1f, 0.99f, 0.95f) };
                case "butter": return new IngredientProfile { name = ingredient, startTemp = 5f, water = 0.16f, coreRate = 0.04f, meltTemp = 32f, raw = new Color(1f, 0.9f, 0.45f), cooked = new Color(1f, 0.82f, 0.3f) };
                case "bread": return new IngredientProfile { name = ingredient, startTemp = 20f, water = 0.35f, coreRate = 0.05f, raw = new Color(0.93f, 0.8f, 0.55f), cooked = new Color(0.93f, 0.8f, 0.55f) };
                default: return new IngredientProfile { name = ingredient };
            }
        }
    }

    /// Continuous cooking state. Pure C#, unit-testable, no recipes.
    [Serializable]
    public class FoodState
    {
        public const float Ambient = 22f;
        public const float BoilPoint = 100f;
        const float ContactRate = 0.08f;   // 1/s surface ↔ hot pan
        const float AirRate = 0.004f;      // 1/s surface ↔ room air

        public IngredientProfile profile;
        public float surfaceTemp, coreTemp;
        public float water;        // remaining surface water 0..1
        public float browning;     // 0..1 Maillard
        public float charred;      // 0..1
        public float set;          // 0..1 protein set (egg) / softened (onion)
        public float melted;       // 0..1 (butter)
        public float secondsHeated;
        public float peakSurface;

        public FoodState(IngredientProfile p)
        {
            profile = p;
            surfaceTemp = coreTemp = p.startTemp;
            water = p.water;
            peakSurface = p.startTemp;
        }

        public FoodState Clone() => (FoodState)MemberwiseClone();

        /// contactTemp: temperature of a hot surface the food is touching, or null if only air.
        public void Step(float dt, float? contactTemp)
        {
            float target = contactTemp ?? Ambient;
            float rate = contactTemp.HasValue ? ContactRate : AirRate;
            float next = surfaceTemp + (target - surfaceTemp) * (1f - Mathf.Exp(-rate * dt));

            // Water pins the surface at 100 °C while it boils off: no browning until the surface dries.
            if (next > BoilPoint && water > 0.02f)
            {
                float excess = (target - BoilPoint) * rate * dt;
                water = Mathf.Max(0f, water - excess * 0.004f);
                next = BoilPoint;
            }
            surfaceTemp = next;
            coreTemp += (surfaceTemp - coreTemp) * (1f - Mathf.Exp(-profile.coreRate * dt));
            peakSurface = Mathf.Max(peakSurface, surfaceTemp);
            if (contactTemp.HasValue && contactTemp.Value > 60f) secondsHeated += dt;

            if (surfaceTemp > 140f) browning = Mathf.Clamp01(browning + 0.004f * (surfaceTemp - 140f) / 40f * dt);
            if (surfaceTemp > 180f) charred = Mathf.Clamp01(charred + 0.003f * (surfaceTemp - 180f) / 40f * dt);
            if (profile.setTemp > 0f && coreTemp > profile.setTemp)
                set = Mathf.Clamp01(set + 0.02f * Mathf.Min(3f, (coreTemp - profile.setTemp) / 5f + 0.2f) * dt);
            if (profile.meltTemp > 0f && surfaceTemp > profile.meltTemp)
                melted = Mathf.Clamp01(melted + 0.01f * (surfaceTemp - profile.meltTemp) / 10f * dt);
        }

        public Color Color
        {
            get
            {
                var c = Color.Lerp(profile.raw, profile.cooked, Mathf.Max(set, melted));
                var golden = new Color(0.82f, 0.52f, 0.2f);
                var brown = new Color(0.42f, 0.24f, 0.09f);
                c = browning < 0.5f ? Color.Lerp(c, golden, browning * 2f) : Color.Lerp(golden, brown, (browning - 0.5f) * 2f);
                return Color.Lerp(c, new Color(0.08f, 0.07f, 0.06f), Mathf.SmoothStep(0f, 1f, charred));
            }
        }

        /// Plain-language doneness, used in the dish summary sent to Claude and in robot-data labels.
        public string Doneness
        {
            get
            {
                string inside = null;
                if (profile.meltTemp > 0f && melted > 0.5f) inside = "melted";
                else if (profile.setTemp > 0f && set > 0.9f) inside = profile.name == "egg" ? "fully set" : "softened";
                else if (profile.setTemp > 0f && set > 0.3f) inside = profile.name == "egg" ? "softly set" : "starting to soften";

                string outside = null;
                if (charred > 0.6f) outside = "burnt";
                else if (charred > 0.2f) outside = "charred in spots";
                else if (browning > 0.75f) outside = "deeply browned";
                else if (browning > 0.4f) outside = "golden brown";
                else if (browning > 0.1f) outside = "lightly browned";

                if (inside != null && outside != null) return inside + ", " + outside;
                if (inside != null || outside != null) return inside ?? outside;
                if (coreTemp > 45f || surfaceTemp > 60f) return "warmed through";
                return "raw";
            }
        }
    }
}
