namespace ChronoClash.Core.Simulation;

public enum GameMode { Classic, Quick, Sandbox, Tutorial }
public enum MapTheme { Temperate, Archipelago, Desert, Winter }
public enum BotDifficulty { Easy, Normal, Hard }

/// <summary>Réglages choisis avant la partie (écran titre ou salon d'attente).</summary>
public sealed record MatchSettings
{
    /// <summary>Vrai pour les modes « calmes » : pas de zone, pas d'événements, bots pacifiques (bac à sable, tutoriel).</summary>
    public bool IsCalm => Mode is GameMode.Sandbox or GameMode.Tutorial;

    /// <summary>Nombre de clans par partie : les places non prises par des humains sont complétées par des bots.</summary>
    public const int MaxClans = 10;

    public uint Seed { get; init; } = 1;
    public GameMode Mode { get; init; } = GameMode.Classic;
    public MapTheme Theme { get; init; } = MapTheme.Temperate;
    public BotDifficulty Difficulty { get; init; } = BotDifficulty.Normal;
    public bool FogOfWar { get; init; } = true;
    /// <summary>Autorise les commandes de test (F4 : +20 troupes). À désactiver pour les vraies parties en ligne.</summary>
    public bool AllowCheats { get; init; }
}
