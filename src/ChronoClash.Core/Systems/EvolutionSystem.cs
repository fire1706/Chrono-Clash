using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Passage aux ères suivantes. Dès qu'un clan atteint le seuil de ★ de l'ère suivante (data/eras.json),
/// son armée évolue sur 20 secondes, troupe par troupe, dans un ordre aléatoire (chef compris).
/// Chaque troupe garde sa proportion de vie et ses chevrons de vétéran.
/// </summary>
public static class EvolutionSystem
{
    public static void Update(World world, float dt, List<GameEvent> events)
    {
        foreach (var army in world.Armies)
        {
            if (!army.Alive) continue;
            if (army.Evolution == null && army.Era < Army.MaxEra && army.Points >= world.Data.Eras[army.Era + 1].Threshold) Start(world, army, events);
            if (army.Evolution != null) Progress(world, army, dt, events);
        }
    }

    private static void Start(World world, Army army, List<GameEvent> events)
    {
        var queue = new List<Body>(army.Units.Where(u => !u.Dead));
        if (!army.General.Dead) queue.Add(army.General);
        // Mélange aléatoire (Fisher-Yates) : l'ordre d'évolution est imprévisible.
        for (int i = queue.Count - 1; i > 0; i--)
        {
            int j = (int)(world.Random.NextDouble() * (i + 1));
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }
        army.Evolution = new EraEvolution(army.Era + 1, queue);
        events.Add(new EraStartedEvent(world.Tick, army.Id, army.Era + 1));
    }

    private static void Progress(World world, Army army, float dt, List<GameEvent> events)
    {
        var evo = army.Evolution!;
        float duration = world.Data.Tuning.Evolution.Duration;
        evo.Elapsed += dt;
        // On répartit les évolutions restantes régulièrement sur le temps qu'il reste.
        float remaining = MathF.Max(0.05f, duration - evo.Elapsed);
        evo.Accumulator += dt * evo.Queue.Count / remaining;
        while (evo.Accumulator >= 1 && evo.Queue.Count > 0)
        {
            evo.Accumulator -= 1;
            EvolveOne(world, evo.Queue[0], evo.TargetEra);
            evo.Queue.RemoveAt(0);
            evo.Done++;
        }
        if (evo.Elapsed < duration) return;
        foreach (var body in evo.Queue) EvolveOne(world, body, evo.TargetEra);
        evo.Queue.Clear();
        army.Era = evo.TargetEra;
        army.Evolution = null;
        army.SlotsDirty = true;
        events.Add(new EraCompletedEvent(world.Tick, army.Id, army.Era));
    }

    private static void EvolveOne(World world, Body body, int era)
    {
        if (body.Dead || body.Era >= era) return;
        if (body is General g) g.ApplyEraStats(world.Data, era);
        else if (body is Unit u)
        {
            float ratio = u.MaxHp > 0 ? u.Hp / u.MaxHp : 1;
            u.Era = era;
            u.MaxHp = world.Data.Stats(u.Kind, era).Hp * (1 + world.Data.Tuning.Veterans.BonusPerLevel * u.Veteran);
            u.Hp = MathF.Max(1, ratio * u.MaxHp);
        }
        body.EvolveFlash = 0.8f;
    }
}
