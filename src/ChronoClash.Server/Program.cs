using ChronoClash.Core;
using ChronoClash.Core.Simulation;

Console.WriteLine($"{CoreInfo.GameName} — serveur {CoreInfo.Version} (protocole {CoreInfo.ProtocolVersion})");

var match = new Match(new MatchSettings { Seed = 42 });
// Simule 5 secondes de jeu le plus vite possible, sans affichage.
for (int i = 0; i < 5 * TickClock.TicksPerSecond; i++) match.Step();

Console.WriteLine($"Ticks simulés : {match.World.Tick} ({match.World.TimeSeconds:0.0} s de jeu)");
