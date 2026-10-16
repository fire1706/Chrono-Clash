using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// État complet du monde à un instant donné : terrain, clans, temps écoulé.
/// Ne contient que des données et des outils de lecture : la logique est dans les systèmes (dossier Systems/).
///
/// En multijoueur (étape M5), chaque client aura sa propre copie de World, mise à jour par les snapshots
/// de l'hôte : l'affichage lit donc toujours un World, en solo comme en réseau.
/// </summary>
public sealed class World
{
    public World(MatchSettings settings)
    {
        Settings = settings;
        Random = new SimRandom(settings.Seed ^ 0x9E3779B9u); // aléatoire de la partie, séparé de celui de la carte
        Terrain = MapGenerator.Generate(settings);
        Nav = new NavGrid(Terrain);
        Pathfinder = new Pathfinder(Terrain, Nav);
        // Les points de départ dépendent seulement de la graine : tous les clients trouvent les mêmes.
        SpawnPlanner.Plan(Terrain, Nav, new SimRandom(settings.Seed + 1), MatchSettings.MaxClans);
    }

    public MatchSettings Settings { get; }

    /// <summary>Aléatoire de la simulation (déterministe à partir de la graine).</summary>
    public SimRandom Random { get; }

    public Terrain Terrain { get; }
    public NavGrid Nav { get; }
    public Pathfinder Pathfinder { get; }

    /// <summary>Tous les clans de la partie (humains et bots), dans l'ordre de leur numéro.</summary>
    public List<Army> Armies { get; } = new();

    /// <summary>Numéro du tick courant depuis le début de la partie.</summary>
    public long Tick { get; internal set; }

    /// <summary>Temps de jeu écoulé, en secondes.</summary>
    public double TimeSeconds => Tick * TickClock.TickDuration;

    /// <summary>Le clan contrôlé par ce joueur, ou null.</summary>
    public Army? FindArmyOfPlayer(int playerId)
    {
        foreach (var a in Armies) if (a.PlayerId == playerId) return a;
        return null;
    }
}
