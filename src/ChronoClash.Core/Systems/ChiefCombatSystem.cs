using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Les chefs combattent aussi : ils frappent l'ennemi le plus proche à leur portée
/// (corps à corps jusqu'en 1500, puis tir à partir de 1914). Hors combat, ils regagnent un peu de vie.
/// </summary>
public static class ChiefCombatSystem
{
    private static readonly List<IBody> Nearby = new();

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        var combat = world.Data.Tuning.Combat;
        double now = world.TimeSeconds;
        foreach (var army in world.Armies)
        {
            var g = army.General;
            if (!army.Alive || g.Dead) continue;
            g.TickVisualTimers(dt);
            if (now - g.LastDamageTime > combat.ChiefRegenDelay && g.Hp < g.MaxHp) g.Hp = MathF.Min(g.MaxHp, g.Hp + combat.ChiefRegenPerSecond * dt);

            g.AttackTimer -= dt;
            if (g.AttackTimer > 0) continue;
            float limit = g.Range > 0 ? g.Range : g.Radius + g.Reach;
            Body? target = null;
            float best = float.MaxValue;
            world.Grid.Query(g.Position.X, g.Position.Y, limit + 20, Nearby);
            foreach (var other in Nearby)
            {
                if (other.IsDead || other.Owner == army || other is not Body body) continue;
                float d = Vec2.Distance(body.Position, g.Position) - (g.Range > 0 ? 0 : body.Radius);
                if (d < limit && d < best) { best = d; target = body; }
            }
            if (target == null) continue;
            if (g.Range > 0) CombatRules.Shoot(world, g, target, g.Projectile ?? "bullet", g.Damage, g.Splash, g.Era, null);
            else
            {
                CombatRules.ApplyDamage(world, army, target, g.Damage, g.Era);
                events.Add(new MeleeHitEvent(world.Tick, target.Position.X, target.Position.Y));
            }
            g.AttackTimer = g.Cooldown;
            g.SwingTime = 0.25f;
        }
    }
}
