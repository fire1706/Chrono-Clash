using Godot;

namespace ChronoClash.Game.Audio;

/// <summary>
/// Fabrique tous les sons du jeu par le calcul, au démarrage (comme le prototype, qui les synthétisait) :
/// pas besoin de fichiers audio pour l'instant. Chaque son est un petit « mélange » d'oscillateurs
/// (sinus, carré, dent de scie, bruit) avec une enveloppe (attaque, chute).
/// Plus tard, un vrai fichier .ogg ou .wav dans game/assets/audio/ pourra remplacer chaque son (voir AudioManager).
/// </summary>
public static class SoundSynth
{
    /// <summary>Fréquence d'échantillonnage : suffisante pour des sons de jeu, et légère.</summary>
    public const int Rate = 22050;

    private static readonly Random Noise = new(1234);

    /// <summary>Un son court : une ou plusieurs couches additionnées.</summary>
    private static float[] Make(float seconds, Func<float, float> sample)
    {
        int n = (int)(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = sample(i / (float)Rate);
        return data;
    }

    // ---- Briques de base ----
    private static float Sine(float t, float f) => MathF.Sin(t * f * MathF.Tau);
    private static float Square(float t, float f) => Sine(t, f) >= 0 ? 1 : -1;
    private static float Saw(float t, float f) { float x = t * f; return 2 * (x - MathF.Floor(x + 0.5f)); }
    private static float White() => (float)(Noise.NextDouble() * 2 - 1);
    /// <summary>Enveloppe : montée rapide en <paramref name="attack"/> s puis chute exponentielle.</summary>
    private static float Env(float t, float attack, float decay) => t < attack ? t / attack : MathF.Exp(-(t - attack) / decay);

    /// <summary>Tous les effets sonores, par nom (le nom sert dans AudioManager).</summary>
    public static Dictionary<string, AudioStreamWav> Effects()
    {
        var s = new Dictionary<string, float[]>
        {
            ["hit"] = Make(0.09f, t => (White() * 0.6f + Sine(t, 110 - t * 400) * 0.6f) * Env(t, 0.002f, 0.025f)),
            ["shoot_bow"] = Make(0.14f, t => White() * 0.35f * Env(t, 0.005f, 0.04f) + Sine(t, 700 - t * 2500) * 0.15f * Env(t, 0.002f, 0.03f)),
            ["shoot_gun"] = Make(0.1f, t => White() * 0.8f * Env(t, 0.001f, 0.018f) + Square(t, 90) * 0.2f * Env(t, 0.001f, 0.03f)),
            ["shoot_plasma"] = Make(0.22f, t => Sine(t, 900 - t * 2600) * 0.45f * Env(t, 0.003f, 0.08f)),
            ["boom"] = Make(0.7f, t => (White() * 0.7f + Sine(t, 55) * 0.6f) * Env(t, 0.004f, 0.22f)),
            ["pop"] = Make(0.12f, t => Sine(t, 520 + t * 1800) * 0.4f * Env(t, 0.003f, 0.05f)),
            ["star"] = Make(0.35f, t => Sine(t, t < 0.1f ? 880 : t < 0.2f ? 1109 : 1319) * 0.35f * Env(t % 0.1f, 0.003f, 0.06f)),
            ["coin"] = Make(0.2f, t => Square(t, t < 0.07f ? 988 : 1319) * 0.18f * Env(t % 0.07f + 0.001f, 0.002f, 0.06f)),
            ["deny"] = Make(0.18f, t => Square(t, 140) * 0.2f * Env(t, 0.004f, 0.08f)),
            ["tactic"] = Make(0.4f, t => (Saw(t, 196) * 0.25f + Saw(t, 294) * 0.2f) * Env(t, 0.02f, 0.18f)),
            ["cry"] = Make(0.7f, t => (Saw(t, 146 + MathF.Sin(t * 30) * 6) * 0.3f + White() * 0.08f) * Env(t, 0.03f, 0.3f)),
            ["evolve"] = Make(0.3f, t => (Sine(t, 1200 + t * 1200) * 0.25f + Sine(t, 1800 + t * 1600) * 0.12f) * Env(t, 0.01f, 0.1f)),
            ["era"] = Make(1.6f, t =>
            {
                // Petite fanfare : trois notes qui montent, puis un accord tenu.
                float f = t < 0.25f ? 392 : t < 0.5f ? 523 : 659;
                float note = t < 0.75f ? (t % 0.25f) : t - 0.75f;
                return (Saw(t, f) * 0.22f + Saw(t, f * 1.5f) * 0.1f) * Env(note, 0.01f, t < 0.75f ? 0.12f : 0.5f);
            }),
            ["zone"] = Make(1.2f, t => Square(t, ((int)(t * 5) % 2 == 0) ? 440 : 330) * 0.16f * Env(t, 0.01f, 0.6f)),
            ["elim"] = Make(0.9f, t => Saw(t, 330 - t * 220) * 0.25f * Env(t, 0.01f, 0.4f)),
            ["death"] = Make(0.12f, t => (Sine(t, 80) * 0.5f + White() * 0.2f) * Env(t, 0.002f, 0.04f)),
            ["chief_down"] = Make(0.8f, t => (Sine(t, 98 - t * 40) * 0.5f + White() * 0.15f) * Env(t, 0.005f, 0.3f)),
            ["ach"] = Make(0.8f, t => (Sine(t, 1047) * 0.25f + Sine(t, 1568) * 0.18f) * Env(t, 0.005f, 0.3f)),
            ["ui"] = Make(0.04f, t => Sine(t, 1500) * 0.25f * Env(t, 0.001f, 0.012f)),
        };
        var result = new Dictionary<string, AudioStreamWav>();
        foreach (var (name, data) in s) result[name] = ToWav(data, loop: false);
        return result;
    }

    /// <summary>
    /// Une boucle musicale de 4 mesures pour chaque ère : tempo, gamme et timbre changent avec l'époque
    /// (tambours et flûte à la Préhistoire… jusqu'aux synthétiseurs du futur). Mélodie tirée au hasard,
    /// mais toujours la même pour une ère (graine fixe).
    /// </summary>
    public static AudioStreamWav Music(int era)
    {
        int[] bpm = { 92, 100, 96, 104, 108, 112, 118, 124 };
        int[][] scales =
        {
            new[] { 0, 3, 5, 7, 10 }, new[] { 0, 2, 3, 5, 7, 8, 10 }, new[] { 0, 2, 3, 5, 7, 9, 10 }, new[] { 0, 2, 4, 5, 7, 9, 11 },
            new[] { 0, 2, 3, 5, 7, 8, 11 }, new[] { 0, 2, 3, 5, 7, 8, 10 }, new[] { 0, 3, 5, 6, 7, 10 }, new[] { 0, 2, 3, 7, 8 },
        };
        float baseNote = 110 * MathF.Pow(2, (era % 4) / 12f);
        float step = 60f / bpm[era] / 4; // une double-croche
        int steps = 64;                   // 4 mesures de 16 pas
        var rng = new Random(1000 + era);
        var melody = new int[steps];
        for (int i = 0; i < steps; i++) melody[i] = rng.NextDouble() < 0.55 ? scales[era][rng.Next(scales[era].Length)] + 12 * rng.Next(0, 2) : -1;
        int[] bassLine = { 0, 0, 5, 3 };
        var data = Make(steps * step, t =>
        {
            int s = Math.Min(steps - 1, (int)(t / step));
            float inStep = t - s * step;
            float v = 0;
            // Batterie : grosse caisse sur les temps, caisse claire sur 2 et 4, charleston (à partir de la Renaissance).
            if (s % 4 == 0) v += Sine(inStep, 60 - inStep * 120) * 0.5f * Env(inStep, 0.002f, 0.06f);
            if (s % 8 == 4) v += White() * 0.25f * Env(inStep, 0.002f, 0.05f);
            if (era >= 3 && s % 2 == 1) v += White() * 0.06f * Env(inStep, 0.001f, 0.015f);
            // Basse : une note par mesure.
            float bassFreq = baseNote / 2 * MathF.Pow(2, bassLine[s / 16 % 4] / 12f);
            v += (era >= 5 ? Saw(t, bassFreq) : Sine(t, bassFreq)) * 0.18f;
            // Mélodie : flûte douce (sinus) aux premières ères, cuivres (dent de scie) au milieu, carré « électronique » à la fin.
            if (melody[s] >= 0)
            {
                float f = baseNote * 2 * MathF.Pow(2, melody[s] / 12f);
                float lead = era <= 1 ? Sine(t, f) : era <= 4 ? Saw(t, f) * 0.6f : Square(t, f) * 0.45f;
                v += lead * 0.16f * Env(inStep, 0.005f, step * 0.8f);
            }
            return v * 0.8f;
        });
        return ToWav(data, loop: true);
    }

    /// <summary>Convertit des échantillons (-1 à 1) en son Godot 16 bits mono.</summary>
    private static AudioStreamWav ToWav(float[] data, bool loop)
    {
        var bytes = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            short v = (short)(Math.Clamp(data[i], -1f, 1f) * 32000);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        var wav = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = bytes };
        if (loop) { wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward; wav.LoopBegin = 0; wav.LoopEnd = data.Length; }
        return wav;
    }
}
