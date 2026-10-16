namespace ChronoClash.Core.Simulation;

/// <summary>
/// Générateur pseudo-aléatoire déterministe (même algorithme « mulberry32 » que le prototype HTML).
/// Une même graine donne exactement la même suite de nombres sur toutes les machines :
/// c'est ce qui permet d'envoyer seulement la graine de la carte en réseau.
/// </summary>
public sealed class SimRandom
{
    private uint _state;

    public SimRandom(uint seed) => _state = seed;

    /// <summary>Entier non signé sur 32 bits.</summary>
    public uint NextUInt()
    {
        unchecked
        {
            _state += 0x6D2B79F5;
            uint t = _state;
            t = (t ^ (t >> 15)) * (1 | t);
            t ^= t + (t ^ (t >> 7)) * (61 | t);
            return t ^ (t >> 14);
        }
    }

    /// <summary>Nombre à virgule dans [0, 1[.</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>Nombre à virgule dans [min, max[.</summary>
    public double Range(double min, double max) => min + (max - min) * NextDouble();

    /// <summary>Entier dans [min, maxExclusive[.</summary>
    public int Range(int min, int maxExclusive)
    {
        if (maxExclusive <= min) return min;
        return min + (int)(NextDouble() * (maxExclusive - min));
    }

    /// <summary>Vrai avec la probabilité donnée (entre 0 et 1).</summary>
    public bool Chance(double probability) => NextDouble() < probability;
}
