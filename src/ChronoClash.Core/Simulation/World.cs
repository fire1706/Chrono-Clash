using ChronoClash.Core.Data;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// État complet du monde à un instant donné : terrain, clans, bulles, temps écoulé.
/// Ne contient que des données et des outils de lecture : la logique est dans les systèmes (dossier Systems/).
///
/// En multijoueur (étape M5), chaque client aura sa propre copie de World, mise à jour par les snapshots
/// de l'hôte : l'affichage lit donc toujours un World, en solo comme en réseau.
/// </summary>
public sealed class World
{
    private int _nextEntityId = 1;

    public World(MatchSettings settings, GameData? data = null)
    {
        Settings = settings;
        Data = data ?? GameData.Default;
        Random = new SimRandom(settings.Seed ^ 0x9E3779B9u); // aléatoire de la partie, séparé de celui de la carte
        Terrain = MapGenerator.Generate(settings);
        Nav = new NavGrid(Terrain);
        Pathfinder = new Pathfinder(Terrain, Nav);
        // Les points de départ dépendent seulement de la graine : tous les clients trouvent les mêmes.
        SpawnPlanner.Plan(Terrain, Nav, new SimRandom(settings.Seed + 1), MatchSettings.MaxClans);
    }

    public MatchSettings Settings { get; }

    /// <summary>Règles chiffrées du jeu (data/*.json).</summary>
    public GameData Data { get; }

    /// <summary>Aléatoire de la simulation (déterministe à partir de la graine).</summary>
    public SimRandom Random { get; }

    public Terrain Terrain { get; }
    public NavGrid Nav { get; }
    public Pathfinder Pathfinder { get; }

    /// <summary>Tous les clans de la partie (humains et bots), dans l'ordre de leur numéro.</summary>
    public List<Army> Armies { get; } = new();

    /// <summary>Bulles de troupes et étoiles présentes sur la carte.</summary>
    public List<Bubble> Bubbles { get; } = new();

    /// <summary>Projectiles en vol.</summary>
    public List<Projectile> Projectiles { get; } = new();

    /// <summary>Morts du tick en cours, traitées à la fin du tick par DeathSystem.</summary>
    public List<Systems.PendingDeath> PendingDeaths { get; } = new();

    /// <summary>Rangement des chefs et troupes par zone (reconstruit à chaque tick).</summary>
    public SpatialGrid Grid { get; } = new();

    /// <summary>Minuteries d'apparition des bulles (utilisées par BubbleSystem).</summary>
    public float TroopBubbleTimer, StarTimer;

    /// <summary>Numéro du tick courant depuis le début de la partie.</summary>
    public long Tick { get; internal set; }

    /// <summary>Temps de jeu écoulé, en secondes.</summary>
    public double TimeSeconds => Tick * TickClock.TickDuration;

    /// <summary>Nouvel identifiant unique (troupes, bulles…).</summary>
    public int NextEntityId() => _nextEntityId++;

    /// <summary>
    /// Crée un clan avec son chef. Sans position, il prend le prochain point de départ libre.
    /// (Utilisé par Match.AddArmy pour les joueurs et les bots, et par les commandes de test.)
    /// </summary>
    public Army CreateArmy(int playerId, string name, uint colorRgb, bool isBot, Mathematics.Vec2? position = null)
    {
        int id = Armies.Count;
        var spawn = position ?? Terrain.SpawnPoints[Math.Min(id, Terrain.SpawnPoints.Count - 1)];
        var general = new General(spawn);
        general.ApplyEraStats(Data, 0);
        float c = Terrain.WorldSize / 2f;
        general.Angle = (new Mathematics.Vec2(c, c) - spawn).Angle; // au départ, le chef regarde vers le centre de la carte
        var army = new Army(id, playerId, name, colorRgb, isBot, general) { FormationAngle = general.Angle };
        Armies.Add(army);
        return army;
    }

    /// <summary>Le clan contrôlé par ce joueur, ou null.</summary>
    public Army? FindArmyOfPlayer(int playerId)
    {
        foreach (var a in Armies) if (a.PlayerId == playerId) return a;
        return null;
    }
}
