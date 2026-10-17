using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Fait voler les projectiles. À l'arrivée :
/// - arme à dégâts de zone (roquette, obus, rocher, plasma) : tous les ennemis dans le rayon sont touchés,
///   moins fort sur les bords (au moins 35 %) ;
/// - autre arme : la cible est touchée si elle est encore près du point d'arrivée (90 % de chances).
/// </summary>
public static class ProjectileSystem
{
    // Liste de travail réutilisée (une par fil d'exécution : plusieurs parties peuvent tourner en parallèle,
    // par exemple les simulations d'équilibrage ou plusieurs parties sur un serveur).
    [ThreadStatic] private static List<IBody>? _nearby;
    private static List<IBody> Nearby => _nearby ??= new();

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        for (int i = world.Projectiles.Count - 1; i >= 0; i--)
        {
            var p = world.Projectiles[i];
            p.Elapsed += dt;
            if (p.Elapsed < p.FlightTime) continue;
            world.Projectiles.RemoveAt(i);
            events.Add(new ProjectileImpactEvent(world.Tick, p.Kind, p.To.X, p.To.Y, p.Splash));

            if (p.Splash > 0)
            {
                world.Grid.Query(p.To.X, p.To.Y, p.Splash + 20, Nearby);
                foreach (var other in Nearby)
                {
                    if (other.IsDead || other.Owner == p.Owner || other is not Body body) continue;
                    float d = Vec2.Distance(body.Position, p.To) - body.Radius;
                    if (d < p.Splash) CombatRules.ApplyDamage(world, p.Owner, body, p.Damage * MathF.Max(0.35f, 1 - MathF.Max(0, d) / p.Splash), p.SourceEra);
                }
                continue;
            }
            var target = p.Target;
            if (!target.Dead && Vec2.Distance(target.Position, p.To) < target.Radius + 20 && world.Random.NextDouble() < 0.9)
                CombatRules.ApplyDamage(world, p.Owner, target, p.Damage, p.SourceEra);
        }
    }
}
