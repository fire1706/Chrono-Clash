using System.Globalization;
using System.Text;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Server;

/// <summary>
/// Résumé lisible d'une série de parties simulées, avec des repères : ce qu'on vise pour chaque mode
/// (durée, ère atteinte…) et des conseils de réglage quand on s'en écarte.
/// </summary>
public static class SimulationReport
{
    /// <summary>Durée visée d'une partie (minutes) : 15 à 20 en classique, 6 à 9 en rapide.</summary>
    private static (double Min, double Max) TargetMinutes(GameMode mode) => mode == GameMode.Quick ? (6, 9) : (15, 20);

    public static string Build(MatchSettings settings, List<MatchResult> results)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        int n = results.Count;
        var durations = results.Select(r => r.DurationSeconds / 60).OrderBy(x => x).ToList();
        double Median(List<double> v) => v.Count == 0 ? 0 : v[v.Count / 2];

        sb.AppendLine($"=== Chrono Clash — {n} parties simulées ({settings.Mode}, bots {settings.Difficulty}) ===");
        sb.AppendLine($"Parties terminées : {results.Count(r => r.Ended)}/{n}");
        sb.AppendLine(string.Format(inv, "Durée (min) : médiane {0:0.0}, min {1:0.0}, max {2:0.0}", Median(durations), durations.FirstOrDefault(), durations.LastOrDefault()));
        sb.AppendLine(string.Format(inv, "Première élimination (min) : médiane {0:0.0}", Median(results.Where(r => r.FirstElimination >= 0).Select(r => r.FirstElimination / 60).OrderBy(x => x).ToList())));
        sb.AppendLine(string.Format(inv, "Phase de zone à la fin : médiane {0}", Median(results.Select(r => (double)r.ZonePhase).OrderBy(x => x).ToList())));

        var allClans = results.SelectMany(r => r.Clans).ToList();
        var winners = results.Select(r => r.Clans.First(c => c.Placement == 1)).ToList();
        sb.AppendLine(string.Format(inv, "Ère du vainqueur : médiane {0}, ère de tous les clans : médiane {1}",
            Median(winners.Select(c => (double)c.Era).OrderBy(x => x).ToList()), Median(allClans.Select(c => (double)c.Era).OrderBy(x => x).ToList())));
        sb.AppendLine("Répartition des ères (tous les clans) : " + string.Join("  ", Enumerable.Range(0, 8).Select(e => $"{e}:{allClans.Count(c => c.Era == e)}")));
        sb.AppendLine(string.Format(inv, "Plus grande armée du vainqueur : médiane {0}", Median(winners.Select(c => (double)c.PeakUnits).OrderBy(x => x).ToList())));
        sb.AppendLine(string.Format(inv, "Par partie : boss abattus {0:0.0}, largages {1:0.0}, portails {2:0.0}, sanctuaires capturés {3:0.0}",
            results.Average(r => r.BossKills), results.Average(r => r.Crates), results.Average(r => r.Portals), results.Average(r => r.Shrines)));
        sb.AppendLine(string.Format(inv, "Vitesse de simulation : {0:0} ticks/s (le jeu en a besoin de 20)", results.Average(r => r.TicksPerSecond)));

        sb.AppendLine("Victoires par personnalité (sur le nombre de clans de cette personnalité) :");
        foreach (var group in allClans.GroupBy(c => c.Personality).OrderBy(g => g.Key))
        {
            int wins = winners.Count(w => w.Personality == group.Key);
            sb.AppendLine(string.Format(inv, "  {0,-15} {1,3} victoires / {2,3} clans ({3:0}%)   place moyenne {4:0.0}",
                group.Key, wins, group.Count(), 100.0 * wins / Math.Max(1, group.Count()), group.Average(c => c.Placement)));
        }

        // ---- Repères et conseils ----
        sb.AppendLine();
        sb.AppendLine("Repères :");
        var (min, max) = TargetMinutes(settings.Mode);
        double median = Median(durations);
        if (median < min) sb.AppendLine($"  ⚠ Parties trop courtes (visé {min}–{max} min) : augmenter zone.phases[].wait ou ai.ramp, ou baisser combat.damageMultiplier.");
        else if (median > max) sb.AppendLine($"  ⚠ Parties trop longues (visé {min}–{max} min) : réduire zone.phases[].wait, ou augmenter combat.damageMultiplier.");
        else sb.AppendLine($"  ✓ Durée dans la cible ({min}–{max} min).");
        double winnerEra = Median(winners.Select(c => (double)c.Era).OrderBy(x => x).ToList());
        double targetEra = settings.Mode == GameMode.Quick ? 3 : 4;
        if (winnerEra < targetEra - 1) sb.AppendLine($"  ⚠ Les vainqueurs évoluent peu (ère médiane {winnerEra}) : baisser les seuils de data/eras.json ou augmenter les ★ (bulles, victimes).");
        else if (winnerEra > targetEra + 2) sb.AppendLine($"  ⚠ Les vainqueurs évoluent très vite (ère médiane {winnerEra}) : augmenter les seuils de data/eras.json.");
        else sb.AppendLine($"  ✓ Progression des ères correcte (ère médiane du vainqueur : {winnerEra}).");
        var rates = allClans.GroupBy(c => c.Personality).Select(g => (g.Key, Rate: winners.Count(w => w.Personality == g.Key) / (double)Math.Max(1, g.Count()))).ToList();
        if (rates.Count > 1 && n >= 10 && rates.Max(r => r.Rate) > 3 * Math.Max(0.02, rates.Min(r => r.Rate)))
            sb.AppendLine($"  ⚠ Personnalité dominante : {rates.MaxBy(r => r.Rate).Key}. Ajuster ses valeurs dans data/bots.json.");
        return sb.ToString();
    }

    /// <summary>Une ligne par clan et par partie, à ouvrir dans un tableur.</summary>
    public static string Csv(List<MatchResult> results)
    {
        var sb = new StringBuilder("seed;duration_min;ended;zone_phase;clan;personality;placement;era;peak_units;kills;points;eliminated_min\n");
        var inv = CultureInfo.InvariantCulture;
        foreach (var r in results)
            foreach (var c in r.Clans)
                sb.AppendLine(string.Join(";", r.Seed, (r.DurationSeconds / 60).ToString("0.00", inv), r.Ended, r.ZonePhase, c.Name, c.Personality,
                    c.Placement, c.Era, c.PeakUnits, c.Kills, c.Points, c.EliminatedAt < 0 ? "" : (c.EliminatedAt / 60).ToString("0.00", inv)));
        return sb.ToString();
    }
}
