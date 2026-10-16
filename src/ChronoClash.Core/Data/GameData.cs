using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChronoClash.Core.Commands;

namespace ChronoClash.Core.Data;

/// <summary>
/// Toutes les règles chiffrées du jeu, lues depuis les fichiers JSON du dossier data/ (troupes, ères, réglages).
/// Pour équilibrer le jeu, on modifie les JSON, pas le code.
/// </summary>
public sealed class GameData
{
    private static GameData? _default;
    private TroopDef[] _troopsByKind = Array.Empty<TroopDef>();

    /// <summary>Les données intégrées au jeu (chargées une seule fois, au premier usage).</summary>
    public static GameData Default => _default ??= LoadEmbedded();

    public List<TroopDef> Troops { get; set; } = new();
    public List<ChiefEraStats> Chiefs { get; set; } = new();
    public StarDef Star { get; set; } = new();
    public List<EraDef> Eras { get; set; } = new();
    public TuningDef Tuning { get; set; } = new();

    /// <summary>Définition d'un type de troupe (guerrier, distance, tank).</summary>
    public TroopDef Troop(TroopKind kind) => _troopsByKind[(int)kind];

    /// <summary>Statistiques d'un type de troupe à une ère donnée.</summary>
    public TroopEraStats Stats(TroopKind kind, int era) => Troop(kind).Eras[Math.Clamp(era, 0, Troop(kind).Eras.Count - 1)];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip, // les // commentaires des fichiers JSON sont ignorés
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Lit les trois fichiers intégrés et vérifie qu'ils sont complets.</summary>
    public static GameData LoadEmbedded()
    {
        var troops = Read<TroopsFile>("troops.json");
        var eras = Read<ErasFile>("eras.json");
        var tuning = Read<TuningDef>("tuning.json");
        var data = new GameData { Troops = troops.Troops, Chiefs = troops.Chiefs, Star = troops.Star, Eras = eras.Eras, Tuning = tuning };
        data.Index();
        return data;
    }

    /// <summary>Range les troupes par type et vérifie les données (message clair si un fichier est incomplet).</summary>
    private void Index()
    {
        _troopsByKind = new TroopDef[3];
        foreach (var t in Troops) _troopsByKind[(int)t.Kind] = t;
        for (int i = 0; i < 3; i++)
        {
            if (_troopsByKind[i] == null) throw new InvalidDataException($"troops.json : il manque le type {(TroopKind)i}");
            if (_troopsByKind[i].Eras.Count != Eras.Count) throw new InvalidDataException($"troops.json : {(TroopKind)i} doit avoir {Eras.Count} ères");
        }
        if (Chiefs.Count != Eras.Count) throw new InvalidDataException($"troops.json : il faut {Eras.Count} chefs (un par ère)");
    }

    private static T Read<T>(string file)
    {
        var assembly = typeof(GameData).Assembly;
        using var stream = assembly.GetManifestResourceStream("ChronoClash.Data." + file)
            ?? throw new FileNotFoundException($"Fichier de données introuvable dans le cœur : data/{file}");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"data/{file} est vide ou invalide");
    }

    private sealed class TroopsFile
    {
        public List<TroopDef> Troops { get; set; } = new();
        public List<ChiefEraStats> Chiefs { get; set; } = new();
        public StarDef Star { get; set; } = new();
    }

    private sealed class ErasFile
    {
        public List<EraDef> Eras { get; set; } = new();
    }
}

/// <summary>Un type de troupe (voir data/troops.json pour la signification de chaque champ).</summary>
public sealed class TroopDef
{
    public TroopKind Kind { get; set; }
    public string Color { get; set; } = "#ffffff";
    public int Cost { get; set; }
    public int Gain { get; set; }
    public int SpawnWeight { get; set; }
    public float Spacing { get; set; }
    public float Radius { get; set; }
    public float BubbleRadius { get; set; }
    public int Points { get; set; }
    public List<TroopEraStats> Eras { get; set; } = new();
}

/// <summary>Statistiques d'un type de troupe à une ère.</summary>
public sealed class TroopEraStats
{
    public string Name { get; set; } = "";
    public string Short { get; set; } = "";
    public float Hp { get; set; }
    public float Damage { get; set; }
    public float Cooldown { get; set; }
    public float Range { get; set; }
    public float Speed { get; set; }
    public float Aggro { get; set; }
    public string? Projectile { get; set; }
    public float Splash { get; set; }
}

/// <summary>Le chef d'une ère.</summary>
public sealed class ChiefEraStats
{
    public string Name { get; set; } = "";
    public float Hp { get; set; }
    public float Damage { get; set; }
    public float Cooldown { get; set; }
    public float SpeedMultiplier { get; set; } = 1;
    public float Reach { get; set; } = 10;
    public float Range { get; set; }
    public string? Projectile { get; set; }
    public float Splash { get; set; }
}

public sealed class StarDef
{
    public string Color { get; set; } = "#f2c94c";
    public float BubbleRadius { get; set; } = 13;
    public int Points { get; set; } = 5;
}

public sealed class EraDef
{
    public string Name { get; set; } = "";
    public string Short { get; set; } = "";
    public int Threshold { get; set; }
    public string Years { get; set; } = "";
    /// <summary>Forme « entrer dans … » pour les annonces (« Ton peuple entre dans l'ère romaine ! »).</summary>
    public string To { get; set; } = "";
}

/// <summary>Réglages d'équilibrage (data/tuning.json).</summary>
public sealed class TuningDef
{
    public BubbleTuning Bubbles { get; set; } = new();
    public CombatTuning Combat { get; set; } = new();
    public TacticsTuning Tactics { get; set; } = new();
    public EvolutionTuning Evolution { get; set; } = new();
    public VeteranTuning Veterans { get; set; } = new();
    public DebugTuning Debug { get; set; } = new();
}

/// <summary>Réglages du combat et des récompenses (voir data/tuning.json pour le sens de chaque champ).</summary>
public sealed class CombatTuning
{
    public float DamageMultiplier { get; set; } = 0.45f;
    public float VictimEraPointBonus { get; set; } = 0.25f;
    public int ChiefPoints { get; set; } = 15;
    public int PromotedChiefPoints { get; set; } = 5;
    public int EliminationPoints { get; set; } = 40;
    public Dictionary<string, int> KillResources { get; set; } = new() { ["Warrior"] = 2, ["Ranged"] = 3, ["Tank"] = 6 };
    public int ChiefResources { get; set; } = 10;
    public int PromotedChiefResources { get; set; } = 3;
    public int EliminationResources { get; set; } = 30;
    public float ChiefRegenPerSecond { get; set; } = 3;
    public float ChiefRegenDelay { get; set; } = 5;
    public float HillRangeMultiplier { get; set; } = 1.25f;
    public float ChargeDamageWarrior { get; set; } = 2.5f;
    public float ChargeDamageTank { get; set; } = 3f;
    public Dictionary<string, float> ProjectileSpeeds { get; set; } = new();

    /// <summary>◆ gagnées en tuant une troupe de ce type.</summary>
    public int ResourcesFor(TroopKind kind) => KillResources.TryGetValue(kind.ToString(), out int v) ? v : 2;

    /// <summary>Vitesse d'un projectile (pixels/s), 600 par défaut si le type est inconnu.</summary>
    public float SpeedOf(string? projectile) => projectile != null && ProjectileSpeeds.TryGetValue(projectile, out float v) ? v : 600;
}

public sealed class TacticDef
{
    public float Cooldown { get; set; }
    public float Duration { get; set; }
    /// <summary>Charge : part des guerriers et tanks qui chargent.</summary>
    public float Share { get; set; } = 0.4f;
    /// <summary>Charge : vitesse ; cri de guerre : bonus de vitesse.</summary>
    public float Speed { get; set; } = 1;
    /// <summary>Cri de guerre : portée autour du chef.</summary>
    public float Radius { get; set; } = 340;
    /// <summary>Cri de guerre : multiplicateur de dégâts.</summary>
    public float Damage { get; set; } = 1;
}

public sealed class TacticsTuning
{
    public TacticDef Charge { get; set; } = new() { Cooldown = 12, Duration = 2.6f, Speed = 1.85f };
    public TacticDef Retreat { get; set; } = new() { Cooldown = 14, Duration = 3 };
    public TacticDef Aim { get; set; } = new() { Cooldown = 12, Duration = 5 };
    public TacticDef WarCry { get; set; } = new() { Cooldown = 25, Duration = 6, Damage = 1.3f, Speed = 1.15f };

    public TacticDef Of(TacticKind kind) => kind switch
    {
        TacticKind.Charge => Charge,
        TacticKind.Retreat => Retreat,
        TacticKind.Aim => Aim,
        _ => WarCry,
    };
}

public sealed class EvolutionTuning
{
    public float Duration { get; set; } = 20;
}

public sealed class VeteranTuning
{
    public int[] FightsForLevel { get; set; } = { 2, 4, 7 };
    public float BonusPerLevel { get; set; } = 0.1f;
}

public sealed class BubbleTuning
{
    public int TroopBubbleTarget { get; set; } = 720;
    public int StarTarget { get; set; } = 60;
    public float TroopSpawnInterval { get; set; } = 0.6f;
    public float StarSpawnInterval { get; set; } = 4f;
    public int NearSpawnCount { get; set; } = 14;
    public float NearSpawnRadius { get; set; } = 520;
    public int PointsPerTroopBubble { get; set; } = 1;
}

public sealed class DebugTuning
{
    public int SpawnTroopsCount { get; set; } = 20;
    public int EnemyTroopsCount { get; set; } = 25;
    public int AddPoints { get; set; } = 50;
}
