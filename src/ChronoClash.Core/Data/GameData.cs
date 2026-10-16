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
}

/// <summary>Réglages d'équilibrage (data/tuning.json).</summary>
public sealed class TuningDef
{
    public BubbleTuning Bubbles { get; set; } = new();
    public DebugTuning Debug { get; set; } = new();
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
}
