using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Événements aléatoires (port du prototype), toutes les 140 à 200 s après les 150 premières secondes :
/// - pluie de météores : 14 impacts annoncés par un cercle rouge (dégâts de zone, parfois une bulle rare) ;
/// - orage : toutes les armées ralentissent pendant 25 s ;
/// - migration de bulles : une longue traînée de bulles traverse la zone sûre.
/// Réglages : data/tuning.json, section "events".
/// </summary>
public static class EventSystem
{
    // Liste de travail réutilisée (une par fil d'exécution : plusieurs parties peuvent tourner en parallèle,
    // par exemple les simulations d'équilibrage ou plusieurs parties sur un serveur).
    [ThreadStatic] private static List<IBody>? _nearby;
    private static List<IBody> Nearby => _nearby ??= new();

    public static void Init(World world) => world.EventTimer = world.Data.Tuning.Events.FirstAfter * world.TimeFactor;

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        var t = world.Data.Tuning.Events;
        if (!world.Settings.IsCalm)
        {
            world.EventTimer -= dt;
            if (world.EventTimer <= 0)
            {
                world.EventTimer = (t.Interval + (float)world.Random.NextDouble() * t.IntervalRandom) * world.TimeFactor;
                double roll = world.Random.NextDouble();
                if (roll < 0.38) StartMeteors(world, events);
                else if (roll < 0.7) { world.StormTime = t.StormDuration; events.Add(new WorldEventStartedEvent(world.Tick, "storm")); }
                else StartMigration(world, events);
            }
        }
        if (world.StormTime > 0)
        {
            world.StormTime -= dt;
            if (world.StormTime <= 0) events.Add(new WorldEventStartedEvent(world.Tick, "stormEnd"));
        }
        UpdateMeteors(world, dt, events);
    }

    private static void StartMeteors(World world, List<GameEvent> events)
    {
        var t = world.Data.Tuning.Events;
        for (int i = 0; i < t.MeteorCount; i++)
            if (world.ZonePoint(0.75f) is { } p) world.Meteors.Add(new Meteor(p, 1.5f + 1.2f * i));
        events.Add(new WorldEventStartedEvent(world.Tick, "meteors"));
    }

    private static void StartMigration(World world, List<GameEvent> events)
    {
        var z = world.Zone;
        bool next = z.NextCenter.HasValue && z.Stage == ZoneStage.Waiting;
        var center = next ? z.NextCenter!.Value : z.Center;
        float radius = next ? z.NextRadius : z.Radius;
        float a = (float)world.Random.NextDouble() * MathF.PI * 2, length = MathF.Min(1600, radius * 1.4f);
        int n = world.Data.Tuning.Events.MigrationBubbles;
        for (int i = 0; i < n; i++)
        {
            float q = i / (float)(n - 1) - 0.5f;
            var p = center + Vec2.FromAngle(a) * (q * length) + new Vec2(((float)world.Random.NextDouble() - 0.5f) * 120, ((float)world.Random.NextDouble() - 0.5f) * 120);
            if (!world.Terrain.IsWalkable(p, 18) || world.Terrain.IsWater(p.X, p.Y)) continue;
            double w = world.Random.NextDouble();
            var kind = w < 0.45 ? BubbleKind.Warrior : w < 0.75 ? BubbleKind.Ranged : w < 0.92 ? BubbleKind.Tank : BubbleKind.Star;
            BubbleSystem.Add(world, kind, p, mature: false);
        }
        events.Add(new WorldEventStartedEvent(world.Tick, "migration"));
    }

    private static void UpdateMeteors(World world, float dt, List<GameEvent> events)
    {
        float damage = world.Data.Tuning.Events.MeteorDamage;
        for (int i = world.Meteors.Count - 1; i >= 0; i--)
        {
            var m = world.Meteors[i];
            m.TimeToImpact -= dt;
            if (m.TimeToImpact > 0) continue;
            world.Meteors.RemoveAt(i);
            events.Add(new MeteorImpactEvent(world.Tick, m.Position.X, m.Position.Y));
            world.Grid.Query(m.Position.X, m.Position.Y, 90, Nearby);
            foreach (var other in Nearby)
                if (!other.IsDead && other is Body body and not Boss && Vec2.Distance(body.Position, m.Position) < 70)
                    CombatRules.ApplyDamage(world, null, body, body.MaxHp * damage, 0);
            // Parfois, le météore laisse une bulle rare (tank ou étoile).
            if (world.Random.NextDouble() < 0.6)
                BubbleSystem.Add(world, world.Random.NextDouble() < 0.5 ? BubbleKind.Tank : BubbleKind.Star, m.Position + new Vec2(20, 10), mature: false);
        }
    }
}
