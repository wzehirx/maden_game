using System;
using UnityEngine;

/// <summary>Resources/Audio içinde klip yoksa kullanılan, kodla sentezlenmiş yedek sesler.</summary>
public static class Synth
{
    const int SR = 22050;

    static AudioClip Make(string name, float seconds, Func<float, float, float> gen, bool normalize = true)
    {
        int n = Mathf.Max(1, (int)(seconds * SR));
        var data = new float[n];
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float v = gen(t, seconds);
            data[i] = v;
            float a = Mathf.Abs(v);
            if (a > peak) peak = a;
        }
        if (normalize)
        {
            float g = 0.9f / peak;
            for (int i = 0; i < n; i++) data[i] *= g;
        }
        var clip = AudioClip.Create(name, n, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Env(float t, float attack, float decay)
    {
        float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
        return a * Mathf.Exp(-t * decay);
    }

    static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);

    // Deterministik gürültü (her klip aynı çıksın diye).
    static System.Random Rng(int seed) => new System.Random(seed);
    static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    /// <summary>Tek kutuplu alçak geçiren gürültü üreteci.</summary>
    class LowNoise
    {
        readonly System.Random _r; readonly float _k; float _y;
        public LowNoise(int seed, float k) { _r = Rng(seed); _k = k; }
        public float Next() { _y += _k * (Noise(_r) - _y); return _y; }
    }

    public static AudioClip Sfx(string name)
    {
        switch (name)
        {
            case "dirt_hit":
            {
                var ln = new LowNoise(1, 0.12f);
                return Make(name, 0.2f, (t, d) => (ln.Next() * 3f + Sine(70f, t) * 0.6f) * Env(t, 0.002f, 22f));
            }
            case "stone_hit":
            {
                var ln = new LowNoise(2, 0.45f);
                return Make(name, 0.22f, (t, d) => (ln.Next() * 1.8f + Sine(190f, t) * 0.5f) * Env(t, 0.001f, 24f));
            }
            case "metal_hit":
                return Make(name, 0.45f, (t, d) =>
                    (Sine(880f, t) + 0.6f * Sine(1370f, t) + 0.4f * Sine(2210f, t)) * Env(t, 0.001f, 11f));
            case "crystal_hit":
                return Make(name, 0.6f, (t, d) =>
                    (Sine(1760f, t) + 0.7f * Sine(2640f, t) + 0.5f * Sine(3520f, t)) * Env(t, 0.001f, 7f));
            case "rare_find":
                return Make(name, 1.2f, (t, d) =>
                {
                    float[] notes = { 880f, 1108.7f, 1318.5f, 1760f };
                    float v = 0f;
                    for (int i = 0; i < notes.Length; i++)
                    {
                        float tt = t - i * 0.11f;
                        if (tt > 0f) v += (Sine(notes[i], tt) + 0.3f * Sine(notes[i] * 2f, tt)) * Env(tt, 0.004f, 4.5f);
                    }
                    return v;
                });
            case "sell":
                return Make(name, 0.5f, (t, d) =>
                    Sine(1568f, t) * Env(t, 0.002f, 14f) + (t > 0.09f ? Sine(2093f, t - 0.09f) * Env(t - 0.09f, 0.002f, 9f) : 0f));
            case "buy":
                return Make(name, 0.45f, (t, d) =>
                {
                    float f = Mathf.Lerp(400f, 900f, t / d);
                    return Sine(f, t) * Env(t, 0.01f, 5f);
                });
            case "error":
                return Make(name, 0.3f, (t, d) =>
                    Mathf.Sign(Sine(110f, t)) * 0.5f * Env(t, 0.005f, 8f) + Sine(146f, t) * 0.3f * Env(t, 0.005f, 8f));
            case "dynamite":
            {
                var ln = new LowNoise(3, 0.08f);
                var hn = new LowNoise(4, 0.6f);
                return Make(name, 1.6f, (t, d) =>
                    (ln.Next() * 5f + Sine(48f, t) * 1.0f) * Env(t, 0.004f, 2.4f) + hn.Next() * Env(t, 0.001f, 14f));
            }
            case "drill_loop":
            {
                var rn = Rng(5);
                // 0.5 sn: tam sayıda periyot => dikişsiz döngü
                return Make(name, 0.5f, (t, d) =>
                {
                    float saw = 2f * ((t * 100f) % 1f) - 1f;
                    float am = 0.65f + 0.35f * Mathf.Sign(Sine(32f, t));
                    return (saw * 0.7f + Noise(rn) * 0.25f) * am;
                });
            }
            case "menu_open":
            {
                var ln = new LowNoise(6, 0.3f);
                return Make(name, 0.3f, (t, d) => ln.Next() * Mathf.Sin(Mathf.PI * t / d) * (0.5f + t / d));
            }
            case "menu_close":
            {
                var ln = new LowNoise(7, 0.3f);
                return Make(name, 0.3f, (t, d) => ln.Next() * Mathf.Sin(Mathf.PI * t / d) * (1.5f - t / d));
            }
            case "fanfare":
                return Make(name, 1.6f, (t, d) =>
                {
                    float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                    float v = 0f;
                    for (int i = 0; i < notes.Length; i++)
                    {
                        float tt = t - i * 0.16f;
                        if (tt > 0f) v += (Sine(notes[i], tt) + 0.4f * Mathf.Sign(Sine(notes[i], tt)) * 0.3f) * Env(tt, 0.01f, i == 3 ? 2.2f : 4f);
                    }
                    return v;
                });
            default:
                return Make(name, 0.1f, (t, d) => Sine(440f, t) * Env(t, 0.002f, 30f));
        }
    }

    /// <summary>Sakin pad müziği (~32 sn). Dosya yoksa kullanılır.</summary>
    public static AudioClip Music(int variant)
    {
        float[][] chords =
        {
            new[] { 220f, 261.63f, 329.63f },   // Am
            new[] { 174.61f, 220f, 261.63f },   // F
            new[] { 261.63f, 329.63f, 392f },   // C
            new[] { 196f, 246.94f, 293.66f }    // G
        };
        if (variant % 2 == 1) Array.Reverse(chords);
        const float chordLen = 8f;
        return Make("music_synth_" + variant, chordLen * chords.Length, (t, d) =>
        {
            int ci = Mathf.Min(chords.Length - 1, (int)(t / chordLen));
            float local = t - ci * chordLen;
            float env = Mathf.Clamp01(local / 2.5f) * Mathf.Clamp01((chordLen - local) / 2.5f + 0.2f);
            float v = 0f;
            foreach (var f in chords[ci]) v += Sine(f, t) + 0.25f * Sine(f * 2f, t);
            v *= 0.5f + 0.15f * Sine(0.2f + variant * 0.05f, t);
            return v * env;
        });
    }

    /// <summary>Katman ortam sesi (6 sn, dikişsiz). Katman arttıkça tını değişir.</summary>
    public static AudioClip Ambient(int layerGroup)
    {
        float seconds = 6f;
        int n = (int)(seconds * SR);
        float k = Mathf.Lerp(0.02f, 0.25f, layerGroup / 4f);
        var ln = new LowNoise(10 + layerGroup, k);
        // Başa dikiş için fazladan 0.6 sn üret, sonunu başa karıştır.
        int extra = (int)(0.6f * SR);
        var raw = new float[n + extra];
        float peak = 0.0001f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)SR;
            float v = ln.Next() * (2f + layerGroup * 0.4f) + 0.15f * Sine(55f + layerGroup * 20f, t);
            raw[i] = v;
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = raw[i];
        for (int i = 0; i < extra; i++)
        {
            float w = i / (float)extra;
            data[i] = data[i] * w + raw[n + i] * (1f - w);
        }
        for (int i = 0; i < n; i++) data[i] *= 0.7f / peak;
        var clip = AudioClip.Create("ambient_synth_" + layerGroup, n, 1, SR, false);
        clip.SetData(data, 0);
        return clip;
    }
}
