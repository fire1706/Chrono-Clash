using System.Text.Json;
using ChronoClash.Core.Simulation;
using ChronoClash.Game.Localization;
using Godot;

namespace ChronoClash.Game.Stats;

/// <summary>Résumé d'une partie terminée, pour les défis et les records.</summary>
public sealed record MatchSummary(GameMode Mode, bool Won, int Placement, int Era, int PeakTroops, int Kills, int BestMultiplier,
    double Survival, int Crates, int Portals, int Shrines, int Bosses, int TacticsUsed);

/// <summary>Un défi à débloquer.</summary>
public sealed record Challenge(string Id, string Name, string Description, Func<MatchSummary, bool> Done);

/// <summary>
/// Les 15 défis et les records personnels, enregistrés dans user://records.json.
/// Seules les parties classiques et rapides comptent (pas le bac à sable ni le tutoriel).
/// Pour ajouter un défi : une ligne dans <see cref="Challenges"/> (et ses traductions dans en.json).
/// </summary>
public sealed class Achievements
{
    private const string SavePath = "user://records.json";

    public static readonly Challenge[] Challenges =
    {
        new("win", "Dernier debout", "Gagner une partie", s => s.Won),
        new("podium", "Sur le podium", "Finir dans les 3 premiers", s => s.Placement <= 3),
        new("rome", "Les légions", "Atteindre l'ère romaine", s => s.Era >= 1),
        new("renaissance", "Renaissance", "Atteindre la Renaissance", s => s.Era >= 3),
        new("future", "Venu du futur", "Atteindre l'ère futuriste", s => s.Era >= 7),
        new("hunter", "Chasseur", "Abattre 50 ennemis en une partie", s => s.Kills >= 50),
        new("scourge", "Fléau des clans", "Abattre 200 ennemis en une partie", s => s.Kills >= 200),
        new("army", "Grande armée", "Avoir 100 troupes en même temps", s => s.PeakTroops >= 100),
        new("colossal", "Armée colossale", "Avoir 250 troupes en même temps", s => s.PeakTroops >= 250),
        new("looter", "Pilleur", "Récupérer 3 largages en une partie", s => s.Crates >= 3),
        new("traveler", "Voyageur temporel", "Traverser 3 portails en une partie", s => s.Portals >= 3),
        new("keeper", "Gardien des sanctuaires", "Capturer 3 sanctuaires en une partie", s => s.Shrines >= 3),
        new("giant", "Tueur de géants", "Abattre un boss", s => s.Bosses >= 1),
        new("underdog", "Outsider", "Abattre un ennemi avec un multiplicateur ×4 ou plus", s => s.BestMultiplier >= 4),
        new("strategist", "Stratège", "Utiliser les 4 tactiques dans la même partie", s => s.TacticsUsed >= 4),
    };

    // ---- Records ----
    public int Played { get; set; }
    public int Wins { get; set; }
    public int BestPlacement { get; set; } = 99;
    public int BestEra { get; set; }
    public int MaxArmy { get; set; }
    public int MaxKills { get; set; }
    public int BestMultiplier { get; set; } = 1;
    public double LongestSurvival { get; set; }
    public HashSet<string> Unlocked { get; set; } = new();

    public static Achievements Load()
    {
        try
        {
            if (!Godot.FileAccess.FileExists(SavePath)) return new();
            using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
            return JsonSerializer.Deserialize<Achievements>(f.GetAsText()) ?? new();
        }
        catch (Exception) { return new(); }
    }

    public void Save()
    {
        using var f = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(this));
    }

    /// <summary>
    /// Enregistre une partie terminée : met à jour les records et renvoie les défis nouvellement débloqués
    /// et les noms des records battus.
    /// </summary>
    public (List<Challenge> NewChallenges, List<string> NewRecords) Record(MatchSummary s)
    {
        var challenges = new List<Challenge>();
        var records = new List<string>();
        if (s.Mode is GameMode.Sandbox or GameMode.Tutorial) return (challenges, records);
        Played++;
        if (s.Won) Wins++;
        void Better(bool beaten, string name, Action apply) { if (beaten) { apply(); records.Add(Loc.T(name)); } }
        Better(s.Placement < BestPlacement, "Meilleure place", () => BestPlacement = s.Placement);
        Better(s.Era > BestEra, "Ère la plus avancée", () => BestEra = s.Era);
        Better(s.PeakTroops > MaxArmy, "Plus grande armée", () => MaxArmy = s.PeakTroops);
        Better(s.Kills > MaxKills, "Plus de victimes", () => MaxKills = s.Kills);
        Better(s.BestMultiplier > BestMultiplier, "Meilleur multiplicateur", () => BestMultiplier = s.BestMultiplier);
        Better(s.Survival > LongestSurvival + 1, "Plus longue survie", () => LongestSurvival = s.Survival);
        foreach (var c in Challenges)
            if (!Unlocked.Contains(c.Id) && c.Done(s)) { Unlocked.Add(c.Id); challenges.Add(c); }
        Save();
        return (challenges, records);
    }
}
