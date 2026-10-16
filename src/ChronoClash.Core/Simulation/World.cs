using ChronoClash.Core.Data;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// État complet du monde à un instant donné : terrain, clans, bulles, zone, objectifs, temps écoulé.
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

        // Réglages qui dépendent du mode et de la difficulté.
        var quick = Data.Tuning.QuickMode;
        Difficulty = Data.Tuning.DifficultyOf(settings.Difficulty);
        bool isQuick = settings.Mode == GameMode.Quick;
        TimeFactor = isQuick ? quick.TimeFactor : 1;
        EraFactor = isQuick ? quick.EraFactor : 1;
        Grace = settings.IsCalm ? float.MaxValue : isQuick ? MathF.Min(Difficulty.Grace, quick.Grace) : Difficulty.Grace;
        HuntRamp = isQuick ? quick.Ramp : Data.Tuning.Ai.Ramp;

        Neutral = new Army(-1, int.MinValue, "Neutre", 0x3B1D1D, isBot: false, new General(Vec2.Zero) { Dead = true }) { Alive = false };
    }

    public MatchSettings Settings { get; }

    /// <summary>Règles chiffrées du jeu (data/*.json).</summary>
    public GameData Data { get; }

    /// <summary>Aléatoire de la simulation (déterministe à partir de la graine).</summary>
    public SimRandom Random { get; }

    public Terrain Terrain { get; }
    public NavGrid Nav { get; }
    public Pathfinder Pathfinder { get; }

    // ---- Réglages de la partie ----
    public DifficultyDef Difficulty { get; }
    /// <summary>Multiplicateur des délais (largages, boss, événements) : 0,35 en partie rapide.</summary>
    public float TimeFactor { get; }
    /// <summary>Multiplicateur des seuils d'ère : 0,45 en partie rapide.</summary>
    public float EraFactor { get; }
    /// <summary>Secondes avant que les bots commencent à chasser.</summary>
    public float Grace { get; }
    /// <summary>Durée pendant laquelle les bots deviennent de plus en plus agressifs.</summary>
    public float HuntRamp { get; }

    // ---- Contenu ----
    /// <summary>Tous les clans de la partie (humains et bots), dans l'ordre de leur numéro.</summary>
    public List<Army> Armies { get; } = new();
    /// <summary>Clan « neutre » auquel appartient le boss (il ne joue pas et n'est pas dans <see cref="Armies"/>).</summary>
    public Army Neutral { get; }
    public List<Bubble> Bubbles { get; } = new();
    public List<Projectile> Projectiles { get; } = new();
    /// <summary>Morts du tick en cours, traitées à la fin du tick par DeathSystem.</summary>
    public List<Systems.PendingDeath> PendingDeaths { get; } = new();
    public SpatialGrid Grid { get; } = new();

    // ---- Zone et carte vivante ----
    public ZoneState Zone { get; } = new();
    public Boss? Boss { get; set; }
    public Crate? Crate { get; set; }
    public List<Portal> Portals { get; } = new();
    public List<Shrine> Shrines { get; } = new();
    public List<Meteor> Meteors { get; } = new();
    /// <summary>Temps restant de l'orage (toutes les armées ralentissent), 0 s'il n'y en a pas.</summary>
    public float StormTime { get; set; }
    /// <summary>Minuteries des objectifs et des événements (utilisées par ObjectiveSystem et EventSystem).</summary>
    public float CrateTimer, BossTimer, PortalTimer, EventTimer;
    /// <summary>Minuteries d'apparition des bulles (utilisées par BubbleSystem).</summary>
    public float TroopBubbleTimer, StarTimer;

    // ---- Brouillard de guerre (une carte de vision par clan, index = Army.Id) ----
    public List<VisionMap> Vision { get; } = new();
    public float VisionTimer;

    // ---- Fin de partie ----
    public bool IsOver { get; set; }
    /// <summary>Clan vainqueur (dernier debout), ou null.</summary>
    public Army? Winner { get; set; }

    /// <summary>Numéro du tick courant depuis le début de la partie.</summary>
    public long Tick { get; internal set; }

    /// <summary>Temps de jeu écoulé, en secondes.</summary>
    public double TimeSeconds => Tick * TickClock.TickDuration;

    /// <summary>Nouvel identifiant unique (troupes, bulles…).</summary>
    public int NextEntityId() => _nextEntityId++;

    /// <summary>Points ★ nécessaires pour entrer dans une ère (réduits en partie rapide).</summary>
    public int EraThreshold(int era) => (int)MathF.Round(Data.Eras[Math.Clamp(era, 0, Data.Eras.Count - 1)].Threshold * EraFactor);

    /// <summary>Multiplicateur de vitesse global (orage).</summary>
    public float WeatherSpeed => StormTime > 0 ? Data.Tuning.Events.StormSpeed : 1f;

    /// <summary>Vrai si ce point est visible pour ce clan (toujours vrai sans brouillard de guerre).</summary>
    public bool IsVisibleTo(Army army, Vec2 p)
    {
        if (!Settings.FogOfWar) return true;
        return army.Id >= 0 && army.Id < Vision.Count && Vision[army.Id].IsVisible(p);
    }

    /// <summary>
    /// Crée un clan avec son chef. Sans position, il prend le prochain point de départ libre.
    /// (Utilisé par Match.AddArmy pour les joueurs et les bots, et par les commandes de test.)
    /// </summary>
    public Army CreateArmy(int playerId, string name, uint colorRgb, bool isBot, Vec2? position = null)
    {
        int id = Armies.Count;
        var spawn = position ?? Terrain.SpawnPoints[Math.Min(id, Terrain.SpawnPoints.Count - 1)];
        var general = new General(spawn);
        general.ApplyEraStats(Data, 0);
        float c = Terrain.WorldSize / 2f;
        general.Angle = (new Vec2(c, c) - spawn).Angle; // au départ, le chef regarde vers le centre de la carte
        var army = new Army(id, playerId, name, colorRgb, isBot, general) { FormationAngle = general.Angle };
        Armies.Add(army);
        Vision.Add(new VisionMap(Data.Tuning.Vision.Cell));
        return army;
    }

    /// <summary>Le clan contrôlé par ce joueur, ou null.</summary>
    public Army? FindArmyOfPlayer(int playerId)
    {
        foreach (var a in Armies) if (a.PlayerId == playerId) return a;
        return null;
    }

    /// <summary>Un point sur la terre ferme, libre, au hasard dans un disque (ou null après 80 essais).</summary>
    public Vec2? DryPoint(Vec2 center, float radius, float clearance = 24)
    {
        for (int k = 0; k < 80; k++)
        {
            float a = (float)Random.NextDouble() * MathF.PI * 2, r = MathF.Sqrt((float)Random.NextDouble()) * radius;
            float x = MathUtil.Clamp(center.X + MathF.Cos(a) * r, 300, Terrain.WorldSize - 300);
            float y = MathUtil.Clamp(center.Y + MathF.Sin(a) * r, 300, Terrain.WorldSize - 300);
            if (Terrain.IsWalkable(x, y, clearance) && !Terrain.IsWater(x, y) && !Nav.IsBlocked(NavGrid.CellOf(x, y))) return new Vec2(x, y);
        }
        return null;
    }

    /// <summary>Un point libre dans la zone sûre (la prochaine zone si elle est connue), à une fraction de son rayon.</summary>
    public Vec2? ZonePoint(float fraction)
    {
        bool next = Zone.NextCenter.HasValue && Zone.Stage == ZoneStage.Waiting;
        var center = next ? Zone.NextCenter!.Value : Zone.Center;
        float radius = next ? Zone.NextRadius : Zone.Radius;
        return DryPoint(center, MathF.Max(120, MathF.Min(radius, Terrain.WorldSize * 0.48f) * fraction));
    }
}
