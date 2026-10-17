using ChronoClash.Core;
using ChronoClash.Core.Simulation;
using ChronoClash.Server;

// Outil en ligne de commande du cœur du jeu (sans Godot).
//
//   dotnet run --project src/ChronoClash.Server -c Release -- simulate --matches 20 --mode classic --difficulty normal --threads 4
//
// Options de « simulate » :
//   --matches N        nombre de parties (20 par défaut)
//   --mode M           classic | quick (classic par défaut)
//   --difficulty D     easy | normal | hard (normal par défaut)
//   --theme T          temperate | archipelago | desert | winter (temperate par défaut)
//   --threads N        parties en parallèle (nombre de cœurs du processeur par défaut)
//   --seed S           graine de départ (les parties utilisent S, S+7919, S+2×7919…) pour refaire exactement les mêmes
//   --max-minutes M    durée maximale d'une partie simulée (40 par défaut)
//   --out fichier.csv  enregistre aussi le détail de chaque clan (à ouvrir dans un tableur)
//
// Le serveur DÉDIÉ pour jouer en ligne, lui, est le jeu Godot lancé sans affichage : voir docs/M7.md.

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"{CoreInfo.GameName} — outils {CoreInfo.Version}");

if (args.Length == 0 || args[0] != "simulate")
{
    Console.WriteLine("Commande : simulate [--matches N] [--mode classic|quick] [--difficulty easy|normal|hard] [--theme ...] [--threads N] [--seed S] [--max-minutes M] [--out fichier.csv]");
    return;
}

string Arg(string name, string fallback)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

int matches = int.Parse(Arg("--matches", "20"));
int threads = int.Parse(Arg("--threads", Environment.ProcessorCount.ToString()));
double maxMinutes = double.Parse(Arg("--max-minutes", "40"), System.Globalization.CultureInfo.InvariantCulture);
var settings = new MatchSettings
{
    Seed = uint.Parse(Arg("--seed", "1000")),
    Mode = Arg("--mode", "classic") == "quick" ? GameMode.Quick : GameMode.Classic,
    Difficulty = Enum.Parse<BotDifficulty>(Arg("--difficulty", "normal"), ignoreCase: true),
    Theme = Enum.Parse<MapTheme>(Arg("--theme", "temperate"), ignoreCase: true),
    FogOfWar = true,
};

Console.WriteLine($"{matches} parties, {threads} en parallèle…");
int done = 0;
var results = SimulationRunner.RunMany(settings, matches, threads, maxMinutes, r =>
{
    done++;
    Console.WriteLine($"  [{done}/{matches}] graine {r.Seed} : {r.DurationSeconds / 60:0.0} min, vainqueur {r.WinnerPersonality}{(r.Ended ? "" : " (non terminée)")}");
});
Console.WriteLine();
Console.WriteLine(SimulationReport.Build(settings, results));

string outFile = Arg("--out", "");
if (outFile != "")
{
    File.WriteAllText(outFile, SimulationReport.Csv(results));
    Console.WriteLine($"Détail enregistré dans {outFile}");
}
