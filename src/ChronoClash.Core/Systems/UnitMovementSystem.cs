using ChronoClash.Core.Commands;
using ChronoClash.Core.Data;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using T = ChronoClash.Core.Systems.UnitTuning;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Comportement de chaque troupe à chaque tick (port de updateArmyUnits du prototype) :
/// 1. elle cherche régulièrement un ennemi proche (ou autour du point visé par la tactique « Viser ») ;
/// 2. elle décide où aller : sa place dans la formation, l'ennemi, ou tout droit si elle charge ;
/// 3. elle attaque si l'ennemi est à portée (coup au corps à corps ou projectile) ;
/// 4. elle se déplace, avec les protections anti-blocage, puis les troupes sont écartées les unes des autres.
/// Elle devient aussi vétéran en survivant aux combats.
/// </summary>
public static class UnitMovementSystem
{
    private static readonly List<IBody> Neighbors = new();

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        world.Grid.Rebuild(world);
        int pathBudget = T.PathBudgetPerTick;
        foreach (var army in world.Armies)
        {
            if (!army.Alive) continue;
            foreach (var u in army.Units)
                if (!u.Dead) UpdateUnit(world, army, u, dt, events, ref pathBudget);
        }
        Separate(world);
    }

    private static void UpdateUnit(World world, Army army, Unit u, float dt, List<GameEvent> events, ref int pathBudget)
    {
        var terrain = world.Terrain;
        var stats = world.Data.Stats(u.Kind, u.Era);
        var tactics = world.Data.Tuning.Tactics;
        var chief = army.General;
        double now = world.TimeSeconds;
        u.PreviousPosition = u.Position;
        u.Age += dt;
        u.AttackTimer -= dt;
        u.TickVisualTimers(dt);
        if (u.WarCryTime > 0) u.WarCryTime -= dt;
        TrackVeteran(world, army, u, events);
        if (army.Bonus.Regen > 0 && u.Hp < u.MaxHp) u.Hp = MathF.Min(u.MaxHp, u.Hp + u.MaxHp * army.Bonus.Regen * dt); // sanctuaire de vie

        // 1. Ma place dans la formation (rapprochée du chef pendant un retrait, et si elle tombe dans un obstacle).
        bool retreating = army.RetreatTime > 0;
        var slot = FormationSystem.SlotWorldPosition(army, u.Slot, retreating ? 0.82f : 1f);
        if (!terrain.IsWalkable(slot, u.Radius))
            for (int k = 1; k <= 12; k++)
            {
                var p = Vec2.Lerp(slot, chief.Position, k / 12f);
                if (terrain.IsWalkable(p, u.Radius)) { slot = p; break; }
            }

        // 2. Choix d'une cible, environ 4 fois par seconde.
        u.ThinkTimer -= dt;
        if (u.ThinkTimer <= 0)
        {
            u.ThinkTimer = 0.22f + (float)world.Random.NextDouble() * 0.12f;
            u.Target = retreating || u.ChargeTime > 0 ? null : FindTarget(world, army, u, stats, slot);
        }
        if (u.Target is { Dead: true }) u.Target = null;

        // 3. Que faire : charger, combattre, ou rejoindre sa place ?
        var target = slot;
        float run = 1;
        bool stop = false, toSlot = true;
        if (u.ChargeTime > 0)
        {
            u.ChargeTime -= dt;
            target = u.Position + army.ChargeDirection * 120;
            run = tactics.Charge.Speed;
            toSlot = false;
            ChargeContacts(world, army, u, stats);
        }
        else if (u.Target != null)
        {
            var enemy = u.Target;
            float d = Vec2.Distance(enemy.Position, u.Position);
            float reach = stats.Range > 0 ? stats.Range * CombatRules.RangeMultiplier(world, u) : u.Radius + enemy.Radius + 4;
            toSlot = false;
            if (d <= reach)
            {
                stop = true;
                u.Angle = MathUtil.TurnTowards(u.Angle, (enemy.Position - u.Position).Angle, 10 * dt);
                if (u.AttackTimer <= 0) Attack(world, army, u, enemy, stats, events);
            }
            else
            {
                target = enemy.Position;
                run = stats.Range > 0 ? 1 : 1.12f;
                // Un tireur presque à portée avance doucement au lieu de foncer au contact.
                if (stats.Range > 0 && d < reach * 1.25f) target = u.Position + (enemy.Position - u.Position) * 0.3f;
            }
        }

        // Suivre un chemin vers sa place (s'il existe et mène toujours au bon endroit).
        if (toSlot && u.Path is { Count: > 0 })
        {
            if (Vec2.Distance(u.Path[^1], slot) > 160 || Vec2.Distance(u.Position, slot) < 40) u.Path = null;
            else
            {
                if (Vec2.Distance(u.Path[0], u.Position) < 14) u.Path.RemoveAt(0);
                if (u.Path.Count > 0) target = u.Path[0]; else u.Path = null;
            }
        }

        // 4. Vitesse et déplacement.
        var delta = target - u.Position;
        float distance = delta.Length, farFromSlot = Vec2.Distance(slot, u.Position);
        float follow = chief.Sprinting ? MovementTuning.SprintMultiplier * T.SprintFollowFactor : retreating ? 0.95f : 1f;
        float cry = u.WarCryTime > 0 ? tactics.WarCry.Speed : 1f;
        float maxSpeed = stats.Speed * follow * cry * army.Bonus.Speed * world.WeatherSpeed * (toSlot && farFromSlot > T.CatchUpDistance ? T.CatchUpMultiplier : run) * terrain.SpeedFactor(u.Position.X, u.Position.Y);
        float wanted = stop ? 0 : MathF.Min(maxSpeed, u.ChargeTime > 0 ? maxSpeed : distance * T.ArrivalBraking);
        var direction = distance > 0.5f ? delta / distance : Vec2.Zero;
        u.Velocity += (direction * wanted - u.Velocity) * MathF.Min(1, T.Acceleration * dt);

        var next = u.Position + u.Velocity * dt;
        if (terrain.IsWalkable(next, u.Radius)) u.Position = next;
        else if (terrain.IsWalkable(next.X, u.Position.Y, u.Radius)) { u.Position = new Vec2(next.X, u.Position.Y); u.Velocity = new Vec2(u.Velocity.X, u.Velocity.Y * 0.5f); }
        else if (terrain.IsWalkable(u.Position.X, next.Y, u.Radius)) { u.Position = new Vec2(u.Position.X, next.Y); u.Velocity = new Vec2(u.Velocity.X * 0.5f, u.Velocity.Y); }
        else u.Velocity *= 0.3f;

        // 5. Anti-blocage.
        float moved = Vec2.Distance(u.Position, u.LastPosition);
        u.LastPosition = u.Position;
        bool blocked = moved < maxSpeed * dt * 0.25f;
        u.StuckTime = toSlot && farFromSlot > T.StuckCheckDistance && blocked ? u.StuckTime + dt : MathF.Max(0, u.StuckTime - dt);
        if (u.StuckTime > T.StuckReplanDelay && u.Path != null) u.Path = null;
        if (u.StuckTime > T.StuckPathDelay && u.Path == null && pathBudget > 0)
        {
            pathBudget--;
            u.StuckTime = 0;
            var path = world.Pathfinder.Plan(u.Position, slot, u.Radius);
            u.Path = path is { Count: > 0 } ? path : null;
        }
        // Un ennemi qu'on n'arrive pas à atteindre (de l'autre côté de l'eau…) : on l'ignore 2,5 s.
        if (!toSlot && u.Target != null && !stop && blocked)
        {
            u.ChaseTime += dt;
            if (u.ChaseTime > 0.7f) { u.Ignored = u.Target; u.IgnoredUntil = now + 2.5; u.Target = null; u.ChaseTime = 0; }
        }
        else u.ChaseTime = 0;
        u.LostTime = farFromSlot > T.LostDistance && blocked ? u.LostTime + dt : MathF.Max(0, u.LostTime - dt);
        if (u.LostTime > T.LostTeleportDelay) RejoinChief(world, u, chief);

        // 6. Orientation et animation de marche.
        float speed = u.Velocity.Length;
        if (!stop) u.Angle = MathUtil.TurnTowards(u.Angle, speed > 18 ? u.Velocity.Angle : army.FormationAngle, T.TurnRate * dt);
        if (speed > 10) u.WalkCycle += speed * dt * (u.Kind == TroopKind.Tank ? 0.045f : 0.09f);
    }

    /// <summary>
    /// L'ennemi le plus intéressant : le plus proche, avec un petit bonus pour garder la même cible et pour viser les chefs.
    /// Sans tactique « Viser », une troupe ne s'éloigne pas à plus de 300 px (+ la moitié de sa portée) de sa place.
    /// </summary>
    private static Body? FindTarget(World world, Army army, Unit u, TroopEraStats stats, Vec2 slot)
    {
        bool aiming = army.AimTime > 0;
        var center = aiming ? army.AimPoint : u.Position;
        float radius = aiming ? MathF.Max(240, stats.Range * 0.6f) : stats.Aggro * (stats.Range > 0 ? CombatRules.RangeMultiplier(world, u) : 1);
        double now = world.TimeSeconds;
        Body? best = null;
        float bestScore = float.MaxValue;
        world.Grid.Query(center.X, center.Y, radius, Neighbors);
        foreach (var other in Neighbors)
        {
            if (other.IsDead || other.Owner == army || other is not Body body) continue;
            if (body == u.Ignored && now < u.IgnoredUntil) continue;
            float d = Vec2.Distance(body.Position, center);
            if (d > radius) continue;
            if (!aiming && Vec2.Distance(body.Position, slot) > 300 + stats.Range * 0.5f) continue;
            float score = d - (body == u.Target ? 30 : 0) - (body.IsChief ? 10 : 0);
            if (score < bestScore) { bestScore = score; best = body; }
        }
        return best;
    }

    /// <summary>Un coup : projectile pour les tireurs, dégâts directs au corps à corps.</summary>
    private static void Attack(World world, Army army, Unit u, Body enemy, TroopEraStats stats, List<GameEvent> events)
    {
        u.AttackTimer = stats.Cooldown * (0.9f + (float)world.Random.NextDouble() * 0.2f);
        u.SwingTime = stats.Projectile == "rock" ? 0.5f : stats.Projectile == "bullet" ? 0.14f : 0.22f;
        u.LastFightTime = world.TimeSeconds;
        float damage = stats.Damage * CombatRules.DamageMultiplier(world, u);
        if (stats.Range > 0 && stats.Projectile != null) CombatRules.Shoot(world, u, enemy, stats.Projectile, damage, stats.Splash, u.Era, u.Kind);
        else
        {
            CombatRules.ApplyDamage(world, army, enemy, damage, u.Era);
            events.Add(new MeleeHitEvent(world.Tick, enemy.Position.X, enemy.Position.Y));
        }
    }

    /// <summary>Pendant une charge : chaque ennemi percuté prend un gros coup (une seule fois) et est repoussé.</summary>
    private static void ChargeContacts(World world, Army army, Unit u, TroopEraStats stats)
    {
        var combat = world.Data.Tuning.Combat;
        world.Grid.Query(u.Position.X, u.Position.Y, u.Radius + 20, Neighbors);
        foreach (var other in Neighbors)
        {
            if (other.IsDead || other.Owner == army || other is not Body enemy || u.ChargeHits.Contains(enemy)) continue;
            float d = Vec2.Distance(enemy.Position, u.Position);
            if (d >= u.Radius + enemy.Radius + 5) continue;
            u.ChargeHits.Add(enemy);
            float factor = u.Kind == TroopKind.Tank ? combat.ChargeDamageTank : combat.ChargeDamageWarrior;
            CombatRules.ApplyDamage(world, army, enemy, stats.Damage * CombatRules.DamageMultiplier(world, u) * factor, u.Era);
            u.LastFightTime = world.TimeSeconds;
            if (!enemy.IsChief)
            {
                float push = u.Kind == TroopKind.Tank ? 14 : 7;
                var pushed = enemy.Position + (enemy.Position - u.Position) / MathF.Max(d, 1) * push;
                if (world.Terrain.IsWalkable(pushed, enemy.Radius)) enemy.Position = pushed;
            }
        }
    }

    /// <summary>
    /// Une troupe est « en combat » si elle a frappé ou été touchée il y a moins de 2 s.
    /// Quand le combat est fini depuis 8 s, elle a survécu à un combat de plus et peut gagner un chevron.
    /// </summary>
    private static void TrackVeteran(World world, Army army, Unit u, List<GameEvent> events)
    {
        double now = world.TimeSeconds, lastAction = Math.Max(u.LastFightTime, u.LastDamageTime);
        if (now - lastAction < 2) { u.InFight = true; return; }
        if (!u.InFight || now - lastAction <= 8) return;
        u.InFight = false;
        u.Fights++;
        var vet = world.Data.Tuning.Veterans;
        int level = 0;
        for (int i = 0; i < vet.FightsForLevel.Length; i++) if (u.Fights >= vet.FightsForLevel[i]) level = i + 1;
        if (level <= u.Veteran) return;
        u.Veteran = level;
        u.MaxHp *= 1 + vet.BonusPerLevel;
        u.Hp = MathF.Min(u.MaxHp, u.Hp + u.MaxHp * 0.25f);
        events.Add(new VeteranEvent(world.Tick, army.Id, u.Position.X, u.Position.Y, level));
    }

    /// <summary>Dernier recours : la troupe réapparaît juste à côté de son chef.</summary>
    private static void RejoinChief(World world, Unit u, General chief)
    {
        for (int k = 0; k < 20; k++)
        {
            float a = (float)world.Random.NextDouble() * MathF.PI * 2, r = 30 + (float)world.Random.NextDouble() * 70;
            var p = chief.Position + Vec2.FromAngle(a) * r;
            if (!world.Terrain.IsWalkable(p, u.Radius)) continue;
            u.Position = u.PreviousPosition = u.LastPosition = p;
            u.Path = null;
            u.LostTime = 0;
            u.EvolveFlash = 0.4f;
            return;
        }
    }

    /// <summary>Écarte les troupes qui se chevauchent (et les repousse hors des chefs, qui eux ne bougent pas).</summary>
    private static void Separate(World world)
    {
        var terrain = world.Terrain;
        foreach (var army in world.Armies)
        {
            if (!army.Alive) continue;
            foreach (var u in army.Units)
            {
                if (u.Dead) continue;
                world.Grid.Query(u.Position.X, u.Position.Y, u.Radius + 16, Neighbors);
                foreach (var other in Neighbors)
                {
                    if (other == u || other.IsDead) continue;
                    bool otherIsChief = other is not Unit; // chef ou boss : il ne recule pas
                    if (!otherIsChief && ((Unit)other).Id <= u.Id) continue; // chaque paire une seule fois
                    var d = other.Position - u.Position;
                    float min = (u.Radius + other.BodyRadius) * T.SeparationFactor, d2 = d.LengthSquared;
                    if (d2 >= min * min || d2 < 1e-4f) continue;
                    float dist = MathF.Sqrt(d2);
                    float push = (min - dist) / (otherIsChief ? 1 : 2);
                    var offset = d / dist * push;
                    if (terrain.IsWalkable(u.Position - offset, u.Radius)) u.Position -= offset;
                    if (other is Unit o && terrain.IsWalkable(o.Position + offset, o.Radius)) o.Position += offset;
                }
            }
        }
    }
}
