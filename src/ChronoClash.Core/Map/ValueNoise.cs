using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Map;

/// <summary>
/// Bruit de valeur 2D lissé (même principe que le prototype) : donne des nombres entre 0 et 1
/// qui varient doucement d'un endroit à l'autre. Sert à générer l'altitude, les forêts et la terre.
/// </summary>
public sealed class ValueNoise
{
    private readonly ushort[] _perm = new ushort[512];
    private readonly float[] _values = new float[256];

    /// <summary>Le bruit est entièrement déterminé par le générateur aléatoire reçu.</summary>
    public ValueNoise(SimRandom random)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        // Mélange de Fisher-Yates, dans le même ordre que le prototype.
        for (int i = 255; i > 0; i--)
        {
            int j = (int)(random.NextDouble() * (i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _perm[i] = (ushort)p[i & 255];
        for (int i = 0; i < 256; i++) _values[i] = (float)random.NextDouble();
    }

    private float Hash(int i, int j) => _values[_perm[_perm[i & 255] + (j & 255)]];

    /// <summary>Bruit lissé à une seule échelle.</summary>
    public double Sample(double x, double y)
    {
        int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
        double xf = x - xi, yf = y - yi;
        double u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf); // courbe douce entre les points
        double a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    /// <summary>
    /// Somme de plusieurs couches de bruit de plus en plus fines (« octaves ») :
    /// les grandes formes du relief plus des détails. Résultat entre 0 et 1.
    /// </summary>
    public double Fbm(double x, double y, int octaves)
    {
        double sum = 0, amplitude = 1, total = 0, frequency = 1;
        for (int i = 0; i < octaves; i++)
        {
            sum += Sample(x * frequency + i * 31.7, y * frequency - i * 17.3) * amplitude;
            total += amplitude;
            amplitude *= 0.5;
            frequency *= 2;
        }
        return sum / total;
    }
}
