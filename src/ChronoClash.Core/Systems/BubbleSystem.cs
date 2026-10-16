using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Bulles de troupes et étoiles : apparition au fil de la partie et ramassage.
/// Une bulle de troupe donne une troupe de son type + des ressources ◆ + 1 point ★.
/// Une étoile donne des points ★. Tout chef ou toute troupe qui touche une bulle la ramasse pour son clan.
/// (Étape M4 : les bulles n'apparaîtront plus que dans la zone sûre.)
/// </summary>
public static class BubbleSystem
{
    private static readonly List<IBody> Nearby = new();

    /// <summary>Placement de départ : des bulles autour de chaque point de départ, puis partout sur la carte.</summary>
    public static void SpawnInitial(World world)
    {
        var tuning = world.Data.Tuning.Bubbles;
        foreach (var spawn in world.Terrain.SpawnPoints)
            for (int i = 0; i < tuning.NearSpawnCount; i++) TrySpawn(world, RandomTroopKind(world), spawn, tuning.NearSpawnRadius, mature: true);
        int nearTotal = world.Terrain.SpawnPoints.Count * tuning.NearSpawnCount;
        for (int i = 0; i < tuning.TroopBubbleTarget - nearTotal; i++) TrySpawn(world, RandomTroopKind(world), null, 0, mature: true);
        for (int i = 0; i < tuning.StarTarget; i++) TrySpawn(world, BubbleKind.Star, null, 0, mature: true);
    }

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        var tuning = world.Data.Tuning.Bubbles;
        foreach (var b in world.Bubbles) b.Age += dt;

        // Réapparition : une bulle à la fois, à intervalle régulier, tant qu'on est sous l'objectif.
        world.TroopBubbleTimer -= dt;
        if (world.TroopBubbleTimer <= 0)
        {
            world.TroopBubbleTimer = tuning.TroopSpawnInterval;
            if (world.Bubbles.Count(b => b.Kind != BubbleKind.Star) < tuning.TroopBubbleTarget) TrySpawn(world, RandomTroopKind(world), null, 0, mature: false);
        }
        world.StarTimer -= dt;
        if (world.StarTimer <= 0)
        {
            world.StarTimer = tuning.StarSpawnInterval;
            if (world.Bubbles.Count(b => b.Kind == BubbleKind.Star) < tuning.StarTarget) TrySpawn(world, BubbleKind.Star, null, 0, mature: false);
        }

        // Ramassage (on parcourt à l'envers pour pouvoir retirer des bulles en cours de route).
        for (int i = world.Bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = world.Bubbles[i];
            world.Grid.Query(bubble.Position.X, bubble.Position.Y, bubble.Radius + 20, Nearby);
            Army? collector = null;
            foreach (var body in Nearby)
            {
                if (body.IsDead || !body.Owner.Alive) continue;
                if (Vec2.Distance(body.Position, bubble.Position) < bubble.Radius + body.BodyRadius + 3) { collector = body.Owner; break; }
            }
            if (collector == null) continue;
            world.Bubbles.RemoveAt(i);
            Collect(world, collector, bubble, events);
        }
    }

    private static void Collect(World world, Army army, Bubble bubble, List<GameEvent> events)
    {
        int resources = 0, points;
        if (bubble.Kind == BubbleKind.Star) points = world.Data.Star.Points;
        else
        {
            var kind = (TroopKind)(int)bubble.Kind;
            resources = world.Data.Troop(kind).Gain;
            points = world.Data.Tuning.Bubbles.PointsPerTroopBubble;
            Recruitment.AddUnit(world, army, kind, bubble.Position);
        }
        army.Resources += resources;
        army.Points += points;
        events.Add(new BubbleCollectedEvent(world.Tick, army.Id, bubble.Kind, bubble.Position.X, bubble.Position.Y, resources, points));
    }

    /// <summary>Type de bulle au hasard selon les fréquences de data/troops.json (guerriers fréquents, tanks rares).</summary>
    private static BubbleKind RandomTroopKind(World world)
    {
        int total = 0;
        foreach (var t in world.Data.Troops) total += t.SpawnWeight;
        double roll = world.Random.NextDouble() * total;
        foreach (var t in world.Data.Troops)
        {
            roll -= t.SpawnWeight;
            if (roll < 0) return (BubbleKind)(int)t.Kind;
        }
        return BubbleKind.Warrior;
    }

    /// <summary>
    /// Essaie de poser une bulle sur la terre ferme (ou l'eau peu profonde non, pour rester visible) :
    /// près d'un point si <paramref name="near"/> est donné, sinon n'importe où.
    /// </summary>
    private static void TrySpawn(World world, BubbleKind kind, Vec2? near, float radius, bool mature)
    {
        float bubbleRadius = kind == BubbleKind.Star ? world.Data.Star.BubbleRadius : world.Data.Troop((TroopKind)(int)kind).BubbleRadius;
        for (int tries = 0; tries < 30; tries++)
        {
            Vec2 p;
            if (near is { } center)
            {
                float a = (float)world.Random.NextDouble() * MathF.PI * 2, r = 120 + (float)world.Random.NextDouble() * radius;
                p = center + Vec2.FromAngle(a) * r;
            }
            else p = new Vec2(200 + (float)world.Random.NextDouble() * (Terrain.WorldSize - 400), 200 + (float)world.Random.NextDouble() * (Terrain.WorldSize - 400));
            if (!world.Terrain.IsWalkable(p, 18) || world.Terrain.IsWater(p.X, p.Y)) continue;
            world.Bubbles.Add(new Bubble(world.NextEntityId(), kind, p, bubbleRadius, (float)world.Random.NextDouble() * 6.28f) { Age = mature ? 1 : 0 });
            return;
        }
    }
}
