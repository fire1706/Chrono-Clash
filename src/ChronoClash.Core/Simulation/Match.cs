using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Systems;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// Une partie complète. C'est le point d'entrée unique de la simulation pour l'hôte
/// (solo, hébergement ou serveur dédié). L'affichage et le réseau ne font que lui parler.
/// </summary>
public sealed class Match
{
    private readonly List<PlayerCommand> _pendingCommands = new();
    private readonly List<PlayerCommand> _commandsThisTick = new();
    private readonly List<GameEvent> _events = new();

    public Match(MatchSettings settings)
    {
        Settings = settings;
        World = new World(settings);
        BubbleSystem.SpawnInitial(World);
        _events.Add(new MatchStartedEvent(0));
    }

    public MatchSettings Settings { get; }
    public World World { get; }
    public TickClock Clock { get; } = new();

    /// <summary>Événements produits depuis le dernier appel à Advance/Step (sons, UI, annonceur).</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>
    /// Ajoute un clan à la partie, sur le prochain point de départ libre.
    /// Renvoie null si la partie est pleine (10 clans).
    /// </summary>
    public Army? AddArmy(int playerId, string name, uint colorRgb, bool isBot)
    {
        if (World.Armies.Count >= MatchSettings.MaxClans) return null;
        return World.CreateArmy(playerId, name, colorRgb, isBot);
    }

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

    /// <summary>
    /// Un tick de simulation. Les systèmes s'exécutent TOUJOURS dans cet ordre (voir docs/ARCHITECTURE.md §3).
    /// Pour ajouter un système, on l'insère ici à sa place.
    /// </summary>
    private void StepOnce()
    {
        float dt = (float)TickClock.TickDuration;

        // On fige la liste des ordres de ce tick (de nouveaux ordres peuvent arriver pendant le calcul).
        _commandsThisTick.Clear();
        _commandsThisTick.AddRange(_pendingCommands);
        _pendingCommands.Clear();

        CommandSystem.Apply(World, _commandsThisTick, _events);   // 1. ordres des joueurs
        // 2. bots (M4)
        TacticSystem.Update(World, dt);                             //    recharges et effets des tactiques
        GeneralMovementSystem.Update(World, dt);                    // 3. déplacement des chefs
        FormationSystem.Update(World);                              // 4. places dans la formation
        UnitMovementSystem.Update(World, dt, _events);              // 5. troupes : cible, attaque, déplacement
        ChiefCombatSystem.Update(World, dt, _events);               // 6. les chefs attaquent
        ProjectileSystem.Update(World, dt, _events);                // 7. projectiles
        DeathSystem.Update(World, _events);                         // 8. morts, succession, éliminations
        EvolutionSystem.Update(World, dt, _events);                 // 9. évolution des ères
        BubbleSystem.Update(World, dt, _events);                    // 10. bulles : apparition et ramassage
        // 11-14. objectifs, zone, vision, score (M4)

        World.Tick++;
    }
}
