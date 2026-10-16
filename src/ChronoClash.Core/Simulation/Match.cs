using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// Une partie complète. C'est le point d'entrée unique de la simulation pour l'hôte
/// (solo, hébergement ou serveur dédié). L'affichage et le réseau ne font que lui parler.
/// </summary>
public sealed class Match
{
    private readonly List<PlayerCommand> _pendingCommands = new();
    private readonly List<GameEvent> _events = new();

    public Match(MatchSettings settings)
    {
        Settings = settings;
        World = new World(settings);
        _events.Add(new MatchStartedEvent(0));
    }

    public MatchSettings Settings { get; }
    public World World { get; }
    public TickClock Clock { get; } = new();

    /// <summary>Événements produits depuis le dernier appel à Advance/Step (sons, UI, annonceur).</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>Ajoute un ordre de joueur (humain ou bot). Il sera appliqué au prochain tick.</summary>
    public void Enqueue(PlayerCommand command) => _pendingCommands.Add(command);

    /// <summary>Fait avancer la partie d'une durée réelle ; renvoie le nombre de ticks simulés.</summary>
    public int Advance(double realSeconds)
    {
        _events.Clear();
        int ticks = Clock.Advance(realSeconds);
        for (int i = 0; i < ticks; i++) StepOnce();
        return ticks;
    }

    /// <summary>Simule exactement un tick (utilisé par les tests et les simulations accélérées).</summary>
    public void Step()
    {
        _events.Clear();
        StepOnce();
    }

    private void StepOnce()
    {
        // M1+ : les systèmes s'exécuteront ici, toujours dans le même ordre (voir docs/ARCHITECTURE.md §3).
        _pendingCommands.Clear();
        World.Tick++;
    }
}
