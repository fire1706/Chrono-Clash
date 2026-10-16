using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using ChronoClash.Core.Systems;

namespace ChronoClash.Core.AI;

/// <summary>
/// Les bots (port de aiThink du prototype). Toutes les ~0,8 s, chaque bot :
/// 1. achète des troupes selon sa personnalité ;
/// 2. met à jour ce qu'il voit (brouillard de guerre honnête, mémoire de 15 s) ;
/// 3. choisit quoi faire, par priorité : rester dans la zone → fuir s'il perd ou si un plus fort approche →
///    chasser une proie plus faible (après la période de grâce) → objectif (largage, portail, sanctuaire, boss) →
///    bulles proches → explorer ;
/// 4. choisit sa formation (colonne pour voyager, formation de combat au contact) et ses tactiques.
/// Il ne fait QUE produire des ordres (<see cref="PlayerCommand"/>), exactement comme un joueur humain.
/// </summary>
public static class BotSystem
{
    private static readonly List<PlayerCommand> Commands = new();
    private static readonly List<IBody> Nearby = new();

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        var tuning = world.Data.Tuning.Ai;
        foreach (var army in world.Armies)
        {
            if (!army.Alive || army.IsPassive || army.Brain is not { } brain) continue;
            brain.ThinkTimer -= dt;
            if (brain.ThinkTimer > 0) continue;
            brain.ThinkTimer = tuning.ThinkInterval * (0.8f + (float)world.Random.NextDouble() * 0.4f);
            Commands.Clear();
            Think(world, army, brain, Commands);
            CommandSystem.Apply(world, Commands, events);
        }
    }

    private static void Think(World world, Army army, BotBrain brain, List<PlayerCommand> cmds)
    {
        var g = army.General;
        if (g.Dead) return;
        var p = brain.Personality;
        var ai = world.Data.Tuning.Ai;
        var diff = world.Difficulty;
        double now = world.TimeSeconds;
        long tick = world.Tick;
        int me = army.PlayerId;

        // ---- 1. Achats ----
        int budget = army.Resources;
        for (int i = 0; i < 4 && budget >= 12; i++)
        {
            double r = world.Random.NextDouble();
            var kind = r < p.Buy[0] ? TroopKind.Warrior : r < p.Buy[0] + p.Buy[1] ? TroopKind.Ranged : TroopKind.Tank;
            if (budget < world.Data.Troop(kind).Cost) kind = kind == TroopKind.Tank && budget >= world.Data.Troop(TroopKind.Ranged).Cost ? TroopKind.Ranged : TroopKind.Warrior;
            if (budget < world.Data.Troop(kind).Cost) break;
            budget -= world.Data.Troop(kind).Cost;
            cmds.Add(new BuyCommand(tick, me, kind));
        }

        // ---- 2. Perception : proies et menaces, d'après ce qu'il voit et ce dont il se souvient ----
        float my = BotSenses.Strength(world, army);
        float f = (float)Math.Clamp(now / world.HuntRamp, 0, 1);
        float huntRange = (ai.HuntRadiusStart + (ai.HuntRadiusEnd - ai.HuntRadiusStart) * f) * p.HuntRange;
        float ratio = (diff.RatioStart + (diff.RatioEnd - diff.RatioStart) * f) * p.Hunt;
        (Vec2 Position, float Distance)? prey = null, threat = null;
        foreach (var other in world.Armies)
        {
            if (other == army || !other.Alive) continue;
            if (BotSenses.Sees(world, army, other) && BotSenses.Anchor(other) is { } seenAt)
                brain.Memory[other.Id] = (seenAt, now, BotSenses.Strength(world, other));
            if (!brain.Memory.TryGetValue(other.Id, out var m) || now - m.SeenAt > ai.MemorySeconds) continue;
            float d = Vec2.Distance(m.Position, g.Position);
            if (d < huntRange && my > m.Strength * ratio && now >= world.Grace && (prey == null || d < prey.Value.Distance)) prey = (m.Position, d);
            if (d < 650 && m.Strength > my * p.Flee && (threat == null || d < threat.Value.Distance)) threat = (m.Position, d);
        }

        // ---- Combat autour du chef : est-on en train de perdre ? ----
        float mine = 0, theirs = 0, ex = 0, ey = 0;
        int enemies = 0;
        Body? enemyChief = null;
        world.Grid.Query(g.Position.X, g.Position.Y, 360, Nearby);
        foreach (var other in Nearby)
        {
            if (other.IsDead || other is Boss || other is not Body body) continue;
            if (body.Owner == army)
            {
                if (body is Unit mu) { var st = world.Data.Stats(mu.Kind, mu.Era); mine += mu.Hp * st.Damage / st.Cooldown; }
                continue;
            }
            float dps = body is General eg ? eg.Damage / MathF.Max(0.1f, eg.Cooldown) : body is Unit eu ? world.Data.Stats(eu.Kind, eu.Era).Damage / world.Data.Stats(eu.Kind, eu.Era).Cooldown : 0;
            theirs += body.Hp * dps;
            ex += body.Position.X; ey += body.Position.Y; enemies++;
            if (body is General && enemyChief == null) enemyChief = body;
        }
        bool losing = enemies >= 3 && theirs > mine * 1.25f;
        var enemyCenter = enemies > 0 ? new Vec2(ex / enemies, ey / enemies) : g.Position;

        // ---- 3. Décision ----
        Vec2? dest = null;
        Vec2? objective = null;
        var safe = ZoneSystem.SafeSpot(world, g.Position);
        if (safe != null && !(prey.HasValue && prey.Value.Distance < 260)) { dest = safe; brain.Mode = BotMode.Zone; }
        else if (losing || threat != null)
        {
            var source = losing ? enemyCenter : threat!.Value.Position;
            var away = g.Position - source;
            for (int k = 0; k < 6 && dest == null; k++)
            {
                float a = away.Angle + ((float)world.Random.NextDouble() - 0.5f) * 1.2f;
                var q = g.Position + Vec2.FromAngle(a) * 520;
                if (world.Terrain.IsWalkable(q, 16) && !world.Zone.IsOutside(q)) dest = q;
            }
            brain.Mode = BotMode.Flee;
        }
        else if (prey != null && (p.Id == "hunter" || (objective = BestObjective(world, army, my, p)) == null)) { dest = prey.Value.Position; brain.Mode = BotMode.Hunt; }
        else if ((objective ??= BestObjective(world, army, my, p)) != null) { dest = objective; brain.Mode = BotMode.Objective; }
        else
        {
            dest = BestBubble(world, g.Position, p.Bubble, ai.BubbleSight);
            if (dest == null && g.Path.Count == 0)
            {
                float far = p.Id == "collector" ? 3200 : 2400;
                for (int k = 0; k < 6 && dest == null; k++)
                {
                    var q = new Vec2(MathUtil.Clamp(g.Position.X + ((float)world.Random.NextDouble() - 0.5f) * far, 300, Terrain.WorldSize - 300),
                                     MathUtil.Clamp(g.Position.Y + ((float)world.Random.NextDouble() - 0.5f) * far, 300, Terrain.WorldSize - 300));
                    if (world.Terrain.IsWalkable(q, 16) && !world.Zone.IsOutside(q)) dest = q;
                }
            }
            brain.Mode = BotMode.Roam;
        }
        if (dest is { } target)
        {
            bool samePlace = g.Path.Count > 0 && Vec2.Distance(g.Path[^1], target) <= 90;
            if (!samePlace) cmds.Add(new MoveToCommand(tick, me, target.X, target.Y, false));
        }

        // Sprint : pour fuir, rentrer dans la zone, ou rattraper une proie proche.
        bool sprint = brain.Mode == BotMode.Flee || (brain.Mode == BotMode.Zone && world.Zone.IsOutside(g.Position)) || (brain.Mode == BotMode.Hunt && prey.HasValue && prey.Value.Distance < 500);
        if (sprint != g.WantsSprint) cmds.Add(new SprintCommand(tick, me, sprint));

        // ---- 4. Formation selon la situation ----
        int wanted = enemies >= 3 || brain.Mode == BotMode.Hunt ? p.Fight
                   : brain.Mode is BotMode.Zone or BotMode.Flee || g.Path.Count > 4 ? p.Travel : p.Idle;
        if (brain.Formation != wanted)
        {
            brain.Formation = wanted;
            cmds.Add(new SelectFormationCommand(tick, me, wanted));
        }

        // ---- Tactiques ----
        if (enemies < 3) return;
        var aim = enemyChief != null && Vec2.Distance(enemyChief.Position, g.Position) < 380 ? enemyChief.Position : enemyCenter;
        float chance = diff.Tactics;
        var cd = army.TacticCooldowns;
        if (losing && cd[(int)TacticKind.Retreat] <= 0 && world.Random.NextDouble() < chance * p.Retreat)
            cmds.Add(new TacticCommand(tick, me, TacticKind.Retreat));
        else if (!losing && brain.Mode != BotMode.Flee && cd[(int)TacticKind.Charge] <= 0 && world.Random.NextDouble() < chance * p.Charge)
            cmds.Add(new TacticCommand(tick, me, TacticKind.Charge, enemyCenter.X, enemyCenter.Y)); // charge vers les ennemis
        else if (cd[(int)TacticKind.WarCry] <= 0 && enemies >= 6 && world.Random.NextDouble() < chance * p.WarCry)
            cmds.Add(new TacticCommand(tick, me, TacticKind.WarCry));
        else if (cd[(int)TacticKind.Aim] <= 0 && army.Units.Count(u => !u.Dead && u.Kind == TroopKind.Ranged) >= 3)
            cmds.Add(new TacticCommand(tick, me, TacticKind.Aim, aim.X, aim.Y));
    }

    /// <summary>
    /// L'objectif le plus intéressant à moins de 1100 px dans la zone : largage (surtout posé), portail
    /// (si pas encore à la dernière ère), sanctuaire qu'il ne tient pas, boss s'il est nettement plus fort que lui.
    /// </summary>
    private static Vec2? BestObjective(World world, Army army, float my, Data.PersonalityDef p)
    {
        var g = army.General.Position;
        Vec2? best = null;
        float bestScore = 1100;
        void Consider(Vec2 at, float bonus)
        {
            if (world.Zone.IsOutside(at)) return;
            float score = Vec2.Distance(at, g) - bonus * p.Objective;
            if (score < bestScore) { bestScore = score; best = at; }
        }
        if (world.Crate is { } crate) Consider(crate.Position, crate.Landed ? 900 : 600);
        if (army.Era < Army.MaxEra) foreach (var portal in world.Portals) Consider(portal.Position, 320);
        foreach (var shrine in world.Shrines) if (shrine.Owner != army) Consider(shrine.Position, 260);
        if (world.Boss is { Dead: false } boss && my > BotSenses.Strength(boss) * 1.4f) Consider(boss.Position, 450);
        return best;
    }

    /// <summary>La bulle la plus intéressante en vue : proche, et de préférence rare (tank, étoile).</summary>
    private static Vec2? BestBubble(World world, Vec2 from, float appetite, float sight)
    {
        Vec2? best = null;
        float bestScore = float.MaxValue;
        var z = world.Zone;
        foreach (var b in world.Bubbles)
        {
            float d = Vec2.Distance(b.Position, from);
            if (d > sight || z.IsOutside(b.Position)) continue;
            // Pas de bulle hors de la PROCHAINE zone une fois que la zone se resserre.
            if (z.NextCenter is { } nc && z.Radius < z.InitialRadius && Vec2.Distance(b.Position, nc) > z.NextRadius) continue;
            float bonus = b.Kind switch { BubbleKind.Tank => 380, BubbleKind.Star => 300, BubbleKind.Ranged => 90, _ => 0 };
            float score = d - bonus * appetite;
            if (score < bestScore) { bestScore = score; best = b.Position; }
        }
        return best;
    }
}
