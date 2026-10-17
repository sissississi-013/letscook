using UnityEngine;

namespace LetsCook
{
    /// Procedural kitchen sound: a sizzle whose loudness follows how much wet food is touching a hot pan,
    /// plus short noise bursts for chops, egg cracks and serving. No audio files needed.
    public class KitchenAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float volume = 0.6f;

        AudioSource sizzle, oneShots;
        AudioClip chop, crack, ding;
        float sizzleLevel;

        public float SizzleLevel => sizzleLevel;

        void Awake()
        {
            sizzle = gameObject.AddComponent<AudioSource>();
            sizzle.clip = Noise("sizzle", 2f, 44100, crackle: true);
            sizzle.loop = true;
            sizzle.volume = 0f;
            sizzle.Play();
            oneShots = gameObject.AddComponent<AudioSource>();
            chop = Burst("chop", 0.09f, 900f);
            crack = Burst("crack", 0.05f, 2500f);
            ding = Tone("ding", 0.5f, 880f);
            CookEvents.Emitted += OnEvent;
        }

        void OnDestroy() => CookEvents.Emitted -= OnEvent;

        void OnEvent(CookEvents.Entry e)
        {
            switch (e.type)
            {
                case "cut": oneShots.PlayOneShot(chop, volume); break;
                case "crack": oneShots.PlayOneShot(crack, volume); break;
                case "plate": oneShots.PlayOneShot(ding, volume * 0.7f); break;
            }
        }

        void Update()
        {
            // Sizzle = sum over food touching a pan hotter than 100 °C, louder while there's still water to boil off.
            float target = 0f;
            var pan = FindFirstObjectByType<Cookware>();
            if (pan != null && pan.temperature > 100f)
            {
                float heat = Mathf.InverseLerp(100f, 220f, pan.temperature);
                foreach (var f in Food.All)
                    if (f.state != null && f.state.surfaceTemp > 90f && f.state.secondsHeated > 0f)
                        target += heat * (0.35f + f.state.water);
            }
            sizzleLevel = Mathf.Lerp(sizzleLevel, Mathf.Clamp01(target), 1f - Mathf.Exp(-4f * Time.deltaTime));
            sizzle.volume = sizzleLevel * volume;
        }

        static AudioClip Noise(string name, float seconds, int rate, bool crackle)
        {
            int n = Mathf.RoundToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rng.NextDouble() * 2 - 1);
                lp += 0.35f * (white - lp);                       // soften the hiss
                float pop = crackle && rng.NextDouble() < 0.0015 ? (float)(rng.NextDouble() * 2 - 1) : 0f;
                data[i] = (white - lp) * 0.35f + pop * 0.8f;      // high-passed hiss + oil pops
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Burst(string name, float seconds, float cutoff)
        {
            const int rate = 44100;
            int n = Mathf.RoundToInt(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0f, a = Mathf.Clamp01(cutoff / rate * 6.28f);
            for (int i = 0; i < n; i++)
            {
                lp += a * ((float)(rng.NextDouble() * 2 - 1) - lp);
                data[i] = lp * Mathf.Exp(-6f * i / n) * 2.5f;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Tone(string name, float seconds, float hz)
        {
            const int rate = 44100;
            int n = Mathf.RoundToInt(seconds * rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                data[i] = (Mathf.Sin(2 * Mathf.PI * hz * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * hz * 2.01f * t)) * 0.3f * Mathf.Exp(-5f * t);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
