using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>Ajout de troupes à une armée : par une bulle, par un achat, ou en test (F4).</summary>
public static class Recruitment
{
    /// <summary>Crée une troupe de l'ère actuelle du clan, à une position donnée.</summary>
    public static Unit AddUnit(World world, Army army, TroopKind kind, Vec2 position)
    {
        var def = world.Data.Troop(kind);
        var stats = world.Data.Stats(kind, army.Era);
        var unit = new Unit(world.NextEntityId(), army, kind, army.Era, position, def.Radius, stats.Hp) { Angle = army.FormationAngle };
        army.Units.Add(unit);
        // Arrivée pendant une évolution : elle évoluera elle aussi avant la fin.
        army.Evolution?.Add(unit);
        army.PeakUnits = Math.Max(army.PeakUnits, army.Units.Count);
        army.SlotsDirty = true;
        return unit;
    }

    /// <summary>Achat d'une troupe avec des ressources ◆ ; elle apparaît juste derrière le chef.</summary>
    public static void TryBuy(World world, Army army, TroopKind kind, List<GameEvent> events)
    {
        int cost = world.Data.Troop(kind).Cost;
        if (army.General.Dead || army.Resources < cost)
        {
            events.Add(new BuyFailedEvent(world.Tick, army.PlayerId, kind, cost, army.Resources));
            return;
        }
        army.Resources -= cost;
        var g = army.General;
        float angle = g.Angle + MathF.PI + ((float)world.Random.NextDouble() - 0.5f);
        var position = g.Position + Vec2.FromAngle(angle) * 30;
        if (!world.Terrain.IsWalkable(position, world.Data.Troop(kind).Radius)) position = g.Position;
        AddUnit(world, army, kind, position);
        events.Add(new TroopRecruitedEvent(world.Tick, army.Id, kind));
    }

    /// <summary>
    /// Test seulement : un clan ennemi immobile (pas encore de bots) apparaît à 650 px devant le chef,
    /// à l'ère demandée, pour essayer le combat, les tactiques et le multiplicateur d'outsider.
    /// </summary>
    public static void SpawnTestEnemy(World world, Army near, int count, int era)
    {
        var g = near.General;
        Vec2 position = g.Position;
        for (int k = 0; k < 40; k++)
        {
            float a = g.Angle + ((float)world.Random.NextDouble() - 0.5f) * 1.2f, d = 560 + (float)world.Random.NextDouble() * 180;
            var p = g.Position + Vec2.FromAngle(a) * d;
            if (world.Terrain.IsWalkable(p, 40)) { position = p; break; }
        }
        int number = world.Armies.Count(a => a.Name.StartsWith("Clan d'entraînement")) + 1;
        var enemy = world.CreateArmy(-1000 - world.Armies.Count, $"Clan d'entraînement {number}", 0x9A9A9A, isBot: true, position);
        enemy.Era = era;
        enemy.General.ApplyEraStats(world.Data, era);
        enemy.General.Hp = enemy.General.MaxHp;
        enemy.General.Angle = enemy.FormationAngle = (g.Position - position).Angle; // il fait face au joueur
        SpawnTestTroops(world, enemy, count);
    }

    /// <summary>Test seulement : ajoute plusieurs troupes au hasard autour du chef.</summary>
    public static void SpawnTestTroops(World world, Army army, int count)
    {
        var g = army.General.Position;
        for (int i = 0; i < count; i++)
        {
            double r = world.Random.NextDouble();
            var kind = r < 0.6 ? TroopKind.Warrior : r < 0.88 ? TroopKind.Ranged : TroopKind.Tank;
            var p = g + Vec2.FromAngle((float)world.Random.NextDouble() * MathF.PI * 2) * (40 + (float)world.Random.NextDouble() * 80);
            if (!world.Terrain.IsWalkable(p, world.Data.Troop(kind).Radius)) p = g;
            AddUnit(world, army, kind, p);
        }
    }
}
