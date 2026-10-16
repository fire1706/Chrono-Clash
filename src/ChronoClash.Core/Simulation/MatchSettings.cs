namespace ChronoClash.Core.Simulation;

public enum GameMode { Classic, Quick, Sandbox, Tutorial }
public enum MapTheme { Temperate, Archipelago, Desert, Winter }
public enum BotDifficulty { Easy, Normal, Hard }

/// <summary>Réglages choisis avant la partie (écran titre ou salon d'attente).</summary>
public sealed record MatchSettings
{
    /// <summary>Nombre de clans par partie : les places non prises par des humains sont complétées par des bots.</summary>
    public const int MaxClans = 10;

    public uint Seed { get; init; } = 1;
    public GameMode Mode { get; init; } = GameMode.Classic;
    public MapTheme Theme { get; init; } = MapTheme.Temperate;
    public BotDifficulty Difficulty { get; init; } = BotDifficulty.Normal;
    public bool FogOfWar { get; init; } = true;
}
