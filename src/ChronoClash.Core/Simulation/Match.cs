using ChronoClash.Core.AI;
using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Systems;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// Une partie complète. C'est le point d'entrée unique de la simulation pour l'hôte
/// (solo, hébergement ou serveur dédié). L'affichage et le réseau ne font que lui parler.
///
/// Déroulé : on crée la partie, on ajoute les joueurs humains (AddArmy), on complète avec des bots (AddBots),
/// puis on fait avancer le temps (Advance ou Step). Les objectifs de la carte sont placés au premier tick,
/// une fois tous les clans connus.
/// </summary>
public sealed class Match
{
    private readonly List<PlayerCommand> _pendingCommands = new();
    private readonly List<PlayerCommand> _commandsThisTick = new();
    private readonly List<GameEvent> _events = new();
    private bool _started;

    public Match(MatchSettings settings)
    {
        Settings = settings;
        World = new World(settings);
        ZoneSystem.Init(World);
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

    /// <summary>
    /// Complète la partie avec des bots (jusqu'à 10 clans), avec les noms et couleurs de data/bots.json
    /// et des personnalités mélangées au hasard. Numéros de joueur des bots : 100, 101, 102…
    /// </summary>
    public void AddBots(int count = MatchSettings.MaxClans)
    {
        var bots = World.Data.Bots;
        var pool = bots.PersonalityPool.ToList();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = (int)(World.Random.NextDouble() * (i + 1));
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        for (int i = 0; i < count && World.Armies.Count < MatchSettings.MaxClans && i < bots.Clans.Count; i++)
        {
            var clan = bots.Clans[i];
            var army = AddArmy(100 + i, clan.Name, clan.ColorRgb, isBot: true)!;
            army.Brain = new BotBrain(bots.Personality(pool[i % pool.Count]), (float)World.Random.NextDouble());
        }
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

    /// <summary>Mise en place au premier tick, quand tous les clans sont là.</summary>
    private void Start()
    {
        _started = true;
        ObjectiveSystem.Init(World);
        EventSystem.Init(World);
        if (Settings.Mode == GameMode.Quick)
            foreach (var army in World.Armies) Recruitment.SpawnTestTroops(World, army, World.Data.Tuning.QuickMode.StartTroops);
        VisionSystem.Update(World, 1);
    }

    /// <summary>
    /// Un tick de simulation. Les systèmes s'exécutent TOUJOURS dans cet ordre (voir docs/ARCHITECTURE.md §3).
    /// Pour ajouter un système, on l'insère ici à sa place.
    /// </summary>
    private void StepOnce()
    {
        if (!_started) Start();
        float dt = (float)TickClock.TickDuration;

        // On fige la liste des ordres de ce tick (de nouveaux ordres peuvent arriver pendant le calcul).
        _commandsThisTick.Clear();
        _commandsThisTick.AddRange(_pendingCommands);
        _pendingCommands.Clear();

        CommandSystem.Apply(World, _commandsThisTick, _events);   // 1. ordres des joueurs
        BotSystem.Update(World, dt, _events);                       // 2. les bots décident (et donnent leurs ordres)
        TacticSystem.Update(World, dt);                             //    recharges et effets des tactiques
        GeneralMovementSystem.Update(World, dt);                    // 3. déplacement des chefs
        FormationSystem.Update(World);                              // 4. places dans la formation
        UnitMovementSystem.Update(World, dt, _events);              // 5. troupes : cible, attaque, déplacement
        ChiefCombatSystem.Update(World, dt, _events);               // 6. les chefs attaquent
        ProjectileSystem.Update(World, dt, _events);                // 7. projectiles
        ObjectiveSystem.Update(World, dt, _events);                 // 8. largages, boss, portails, sanctuaires
        EventSystem.Update(World, dt, _events);                     //    météores, orage, migrations
        ZoneSystem.Update(World, dt, _events);                      // 9. zone battle royale
        DeathSystem.Update(World, _events);                         // 10. morts, succession, éliminations
        DeathSystem.CheckMatchEnd(World, _events);                  //     dernier clan debout ?
        EvolutionSystem.Update(World, dt, _events);                 // 11. évolution des ères
        BubbleSystem.Update(World, dt, _events);                    // 12. bulles : apparition et ramassage
        VisionSystem.Update(World, dt);                             // 13. brouillard de guerre

        World.Tick++;
    }
}
