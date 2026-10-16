using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Traite les morts du tick :
/// - récompenses du tueur : ★ (valeur de la victime × bonus d'ère de la victime × multiplicateur d'outsider) et ◆ ;
/// - mort d'un chef : la troupe la plus proche (guerrier d'abord) prend le commandement, avec ses propres statistiques ;
/// - un clan sans chef ni troupe est éliminé (bonus d'élimination pour le clan qui l'a touché en dernier) ;
/// - les troupes mortes sont retirées des armées.
/// </summary>
public static class DeathSystem
{
    public static void Update(World world, List<GameEvent> events)
    {
        if (world.PendingDeaths.Count == 0) return;
        var deaths = world.PendingDeaths.ToList();
        world.PendingDeaths.Clear();
        var combat = world.Data.Tuning.Combat;

        foreach (var (victim, killer, sourceEra) in deaths)
        {
            if (victim is Boss boss) { ObjectiveSystem.OnBossKilled(world, boss, killer, events); continue; }
            var army = victim.Owner;
            var chief = victim as General;
            var unit = victim as Unit;
            int points = 0, resources = 0, multiplier = 1;
            if (killer != null && killer.Alive && killer != army)
            {
                int baseValue = chief != null ? (chief.Promoted ? combat.PromotedChiefPoints : combat.ChiefPoints) : world.Data.Troop(unit!.Kind).Points;
                baseValue = (int)MathF.Round(baseValue * (1 + victim.Era * combat.VictimEraPointBonus));
                multiplier = CombatRules.UnderdogMultiplier(sourceEra, victim.Era);
                points = baseValue * multiplier;
                resources = chief != null ? (chief.Promoted ? combat.PromotedChiefResources : combat.ChiefResources) : combat.ResourcesFor(unit!.Kind);
                killer.Points += points;
                killer.Resources += resources;
                killer.Kills++;
            }
            events.Add(new BodyDiedEvent(world.Tick, army.Id, chief != null, unit?.Kind, victim.Position.X, victim.Position.Y, killer?.Id ?? -1, points, resources, multiplier));

            if (chief != null) Promote(world, army, events);
            else army.SlotsDirty = true;
            CheckElimination(world, army, events);
        }
        foreach (var army in world.Armies) army.Units.RemoveAll(u => u.Dead);
    }

    /// <summary>Succession : la troupe vivante la plus proche du chef prend sa place (guerrier, sinon distance, sinon tank).</summary>
    private static bool Promote(World world, Army army, List<GameEvent> events)
    {
        var g = army.General;
        Unit? heir = null;
        foreach (var kind in new[] { TroopKind.Warrior, TroopKind.Ranged, TroopKind.Tank })
        {
            heir = army.Units.Where(u => !u.Dead && u.Kind == kind).OrderBy(u => Mathematics.Vec2.Distance(u.Position, g.Position)).FirstOrDefault();
            if (heir != null) break;
        }
        if (heir == null) return false;

        heir.Dead = true; // le soldat quitte les rangs (sans compter comme une mort)
        g.Dead = false;
        g.Position = g.PreviousPosition = g.LastPosition = heir.Position;
        g.Velocity = Mathematics.Vec2.Zero;
        g.Path.Clear();
        g.Promoted = true;
        g.HeirKind = heir.Kind;
        g.Promotions++;
        g.ApplyEraStats(world.Data, heir.Era);
        g.Hp = g.MaxHp;
        g.AttackTimer = 0.5f;
        g.LastDamageTime = world.TimeSeconds;
        g.EvolveFlash = 0.8f;
        if (army.Evolution != null && g.Era < army.Evolution.TargetEra) army.Evolution.Add(g);
        army.SlotsDirty = true;
        events.Add(new ChiefPromotedEvent(world.Tick, army.Id, heir.Kind));
        return true;
    }

    private static void CheckElimination(World world, Army army, List<GameEvent> events)
    {
        if (!army.Alive || !army.General.Dead || army.Units.Any(u => !u.Dead)) return;
        army.Alive = false;
        army.EliminatedAt = world.TimeSeconds;
        army.Placement = world.Armies.Count(a => a.Alive) + 1; // ex. dernier des 10 = 10e
        var killer = army.LastHitBy;
        if (killer != null && killer.Alive && killer != army)
        {
            killer.Points += world.Data.Tuning.Combat.EliminationPoints;
            killer.Resources += world.Data.Tuning.Combat.EliminationResources;
        }
        events.Add(new ArmyEliminatedEvent(world.Tick, army.Id, killer?.Id ?? -1, world.Armies.Count(a => a.Alive)));
    }

    /// <summary>Fin de partie : quand il ne reste qu'un clan (sur au moins deux au départ), il gagne.</summary>
    public static void CheckMatchEnd(World world, List<GameEvent> events)
    {
        if (world.IsOver || world.Armies.Count < 2) return;
        var alive = world.Armies.Where(a => a.Alive && !a.IsPassive).ToList();
        if (alive.Count > 1) return;
        world.IsOver = true;
        world.Winner = alive.FirstOrDefault();
        if (world.Winner != null) world.Winner.Placement = 1;
        events.Add(new MatchEndedEvent(world.Tick, world.Winner?.Id ?? -1));
    }
}
