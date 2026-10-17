using System.Diagnostics;
using ChronoClash.Core.Events;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Server;

/// <summary>Ce qu'est devenu un clan à la fin d'une partie simulée.</summary>
public sealed record ClanResult(string Name, string Personality, int Placement, int Era, int PeakUnits, int Kills, int Points, double EliminatedAt);

/// <summary>Résultat d'une partie simulée (10 bots, sans affichage).</summary>
public sealed record MatchResult(uint Seed, double DurationSeconds, bool Ended, string WinnerPersonality, int ZonePhase,
    int BossKills, int Crates, int Portals, int Shrines, double FirstElimination, double TicksPerSecond, List<ClanResult> Clans);

/// <summary>
/// Simule des parties complètes entre 10 bots, le plus vite possible et sans affichage, pour vérifier l'équilibrage :
/// durée des parties, ères atteintes, taux de victoire de chaque personnalité, usage des objectifs…
/// C'est le même code que le jeu : un réglage changé dans data/*.json se retrouve directement dans les résultats.
/// </summary>
public static class SimulationRunner
{
    public static MatchResult Run(MatchSettings settings, double maxMinutes)
    {
        var match = new Match(settings);
        match.AddBots(); // 10 bots (le 10e clan de data/bots.json ne sert qu'ici)
        var world = match.World;
        int bossKills = 0, crates = 0, portals = 0, shrines = 0;
        double firstElimination = -1;
        var clock = Stopwatch.StartNew();
        long maxTicks = (long)(maxMinutes * 60 * TickClock.TicksPerSecond);
        while (!world.IsOver && world.Tick < maxTicks)
        {
            match.Step();
            foreach (var e in match.Events)
            {
                switch (e)
                {
                    case BossKilledEvent: bossKills++; break;
                    case CrateCollectedEvent: crates++; break;
                    case PortalUsedEvent: portals++; break;
                    case ShrineCapturedEvent: shrines++; break;
                    case ArmyEliminatedEvent when firstElimination < 0: firstElimination = world.TimeSeconds; break;
                }
            }
        }
        double tps = world.Tick / Math.Max(0.001, clock.Elapsed.TotalSeconds);
        var clans = world.Armies.Select(a => new ClanResult(
            a.Name, a.Brain?.Personality.Name ?? "-", a.Placement == 0 ? 1 : a.Placement, a.Era, a.PeakUnits, a.Kills, a.Points, a.EliminatedAt)).ToList();
        return new MatchResult(settings.Seed, world.TimeSeconds, world.IsOver, world.Winner?.Brain?.Personality.Name ?? "-",
            world.Zone.Phase, bossKills, crates, portals, shrines, firstElimination, tps, clans);
    }

    /// <summary>Lance plusieurs parties, en parallèle sur plusieurs cœurs du processeur si demandé.</summary>
    public static List<MatchResult> RunMany(MatchSettings template, int count, int threads, double maxMinutes, Action<MatchResult>? progress = null)
    {
        _ = Core.Data.GameData.Default; // chargé une fois avant de lancer les fils d'exécution
        var results = new MatchResult[count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, threads) };
        object gate = new();
        Parallel.For(0, count, options, i =>
        {
            var result = Run(template with { Seed = template.Seed + (uint)i * 7919u }, maxMinutes);
            results[i] = result;
            lock (gate) progress?.Invoke(result);
        });
        return results.ToList();
    }
}
