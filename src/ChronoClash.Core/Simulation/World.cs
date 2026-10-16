namespace ChronoClash.Core.Simulation;

/// <summary>
/// État complet du monde à un instant donné. Ne contient que des données :
/// la logique est dans les systèmes (dossier Systems/, à partir de M1).
/// </summary>
public sealed class World
{
    public World(MatchSettings settings)
    {
        Settings = settings;
        Random = new SimRandom(settings.Seed);
    }

    public MatchSettings Settings { get; }

    /// <summary>Aléatoire de la simulation (déterministe à partir de la graine).</summary>
    public SimRandom Random { get; }

    /// <summary>Numéro du tick courant depuis le début de la partie.</summary>
    public long Tick { get; internal set; }

    /// <summary>Temps de jeu écoulé, en secondes.</summary>
    public double TimeSeconds => Tick * TickClock.TickDuration;
}
