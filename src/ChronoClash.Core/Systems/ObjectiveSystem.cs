using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// La carte vivante (port du prototype) :
/// - largages : annoncés 20 s à l'avance, le premier clan qui touche la caisse gagne 60 ◆, 40 ★ et 4 troupes ;
/// - boss neutre : apparaît dans des ruines, garde son repaire, rapporte gros à qui l'abat ;
/// - portails temporels : font passer jusqu'à 8 troupes à l'ère suivante ;
/// - sanctuaires (vent, vie, puissance) : capturés en restant seul 4 s dans le cercle, bonus permanent tant qu'on les tient.
/// Réglages : data/tuning.json, section "objectives".
/// </summary>
public static class ObjectiveSystem
{
    private static readonly List<IBody> Nearby = new();

    /// <summary>Place les sanctuaires et les premiers portails (un près de chaque point de départ). À appeler après l'ajout des clans.</summary>
    public static void Init(World world)
    {
        var tuning = world.Data.Tuning.Objectives;
        world.CrateTimer = tuning.Crate.FirstDelay * world.TimeFactor;
        world.BossTimer = tuning.Boss.FirstDelay * world.TimeFactor;
        float c = Terrain.WorldSize / 2f;

        // 7 sanctuaires : 3 sur des ruines, les autres répartis sur la carte, bien espacés.
        var kinds = new[] { ShrineKind.Wind, ShrineKind.Life, ShrineKind.Power, ShrineKind.Wind, ShrineKind.Life, ShrineKind.Power, ShrineKind.Life };
        var spots = world.Terrain.Ruins.OrderBy(_ => world.Random.NextDouble()).Take(3).Select(r => new Vec2(r.X, r.Y)).ToList();
        for (int k = 0; k < 400 && spots.Count < kinds.Length; k++)
        {
            var p = world.DryPoint(new Vec2(c, c), Terrain.WorldSize * 0.46f, 40);
            if (p is { } q && spots.All(s => Vec2.Distance(s, q) > 1500)) spots.Add(q);
        }
        for (int i = 0; i < spots.Count; i++) world.Shrines.Add(new Shrine(world.NextEntityId(), kinds[i], spots[i]));

        // Un portail à quelques centaines de pixels de chaque clan, plus 3 au hasard.
        foreach (var army in world.Armies)
            for (int k = 0; k < 10; k++)
            {
                var p = world.DryPoint(army.General.Position, 1200, 30);
                if (p is { } q && Vec2.Distance(q, army.General.Position) > 450) { AddPortal(world, q); break; }
            }
        for (int i = 0; i < 3; i++) SpawnPortal(world);
    }

    private static void AddPortal(World world, Vec2 p) => world.Portals.Add(new Portal(world.NextEntityId(), p, (float)world.Random.NextDouble() * 6));

    private static void SpawnPortal(World world)
    {
        var p = world.DryPoint(world.Zone.Center, MathF.Min(world.Zone.Radius, Terrain.WorldSize * 0.48f) * 0.8f, 30);
        if (p is { } q) AddPortal(world, q);
    }

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        var tuning = world.Data.Tuning.Objectives;
        UpdateBonuses(world);
        UpdateCrate(world, dt, events);
        UpdateBoss(world, dt, events);

        // Portails
        foreach (var portal in world.Portals.ToList())
        {
            portal.Phase += dt * 2;
            var army = FirstToucher(world, portal.Position, 24);
            if (army != null) UsePortal(world, army, portal, events);
        }
        world.PortalTimer -= dt;
        if (world.Portals.Count < tuning.Portals.Max && world.PortalTimer <= 0)
        {
            SpawnPortal(world);
            world.PortalTimer = tuning.Portals.Interval;
        }

        UpdateShrines(world, dt, events);
    }

    /// <summary>Bonus de chaque clan selon les sanctuaires qu'il tient (cumulables).</summary>
    private static void UpdateBonuses(World world)
    {
        var s = world.Data.Tuning.Objectives.Shrines;
        foreach (var army in world.Armies)
        {
            int wind = 0, life = 0, power = 0;
            foreach (var shrine in world.Shrines)
            {
                if (shrine.Owner != army) continue;
                if (shrine.Kind == ShrineKind.Wind) wind++;
                else if (shrine.Kind == ShrineKind.Life) life++;
                else power++;
            }
            army.Bonus = new ArmyBonus(1 + s.WindSpeed * wind, s.LifeRegen * life, 1 + s.PowerDamage * power);
        }
    }

    /// <summary>Le premier clan dont un chef ou une troupe touche ce cercle (le boss ne compte pas).</summary>
    private static Army? FirstToucher(World world, Vec2 p, float radius)
    {
        world.Grid.Query(p.X, p.Y, radius + 20, Nearby);
        foreach (var body in Nearby)
            if (!body.IsDead && body.Owner.Alive && Vec2.Distance(body.Position, p) < radius + body.BodyRadius) return body.Owner;
        return null;
    }

    // ================= Largages =================

    private static void UpdateCrate(World world, float dt, List<GameEvent> events)
    {
        var t = world.Data.Tuning.Objectives.Crate;
        world.CrateTimer -= dt;
        if (world.Crate == null && world.CrateTimer <= 0)
        {
            var p = world.ZonePoint(0.6f);
            if (p is { } q)
            {
                world.Crate = new Crate(q, t.Warning);
                events.Add(new CrateAnnouncedEvent(world.Tick, q.X, q.Y, t.Warning));
            }
            world.CrateTimer = (t.Interval + (float)world.Random.NextDouble() * t.IntervalRandom) * world.TimeFactor;
        }
        var crate = world.Crate;
        if (crate == null) return;
        if (!crate.Landed) { crate.TimeToLand -= dt; return; }
        var army = FirstToucher(world, crate.Position, 22);
        if (army == null) return;
        world.Crate = null;
        army.Resources += t.Resources;
        army.Points += t.Points;
        foreach (var kind in t.Troops)
        {
            float a = (float)world.Random.NextDouble() * MathF.PI * 2;
            var p = crate.Position + Vec2.FromAngle(a) * 30;
            if (!world.Terrain.IsWalkable(p, world.Data.Troop(kind).Radius)) p = crate.Position;
            Recruitment.AddUnit(world, army, kind, p);
        }
        events.Add(new CrateCollectedEvent(world.Tick, army.Id, crate.Position.X, crate.Position.Y));
    }

    // ================= Boss =================

    private static void UpdateBoss(World world, float dt, List<GameEvent> events)
    {
        var t = world.Data.Tuning.Objectives.Boss;
        if (world.Boss == null)
        {
            world.BossTimer -= dt;
            if (world.BossTimer <= 0) SpawnBoss(world, events);
            return;
        }
        var b = world.Boss;
        b.PreviousPosition = b.Position;
        b.AttackTimer -= dt;
        b.TickVisualTimers(dt);
        b.ThinkTimer -= dt;
        if (b.ThinkTimer <= 0)
        {
            // Il vise le corps le plus proche, mais seulement s'il ne l'éloigne pas trop de son repaire.
            b.ThinkTimer = 0.4f;
            Body? best = null;
            float bestDistance = float.MaxValue;
            world.Grid.Query(b.Position.X, b.Position.Y, t.Aggro, Nearby);
            foreach (var other in Nearby)
            {
                if (other.IsDead || other is Boss || !other.Owner.Alive || other is not Body body) continue;
                float d = Vec2.Distance(body.Position, b.Position);
                if (d < bestDistance) { bestDistance = d; best = body; }
            }
            b.Target = best != null && Vec2.Distance(best.Position, b.Lair) < t.Leash ? best : null;
        }
        var target = b.Lair;
        bool stop = false;
        if (b.Target is { Dead: false } enemy)
        {
            target = enemy.Position;
            if (Vec2.Distance(enemy.Position, b.Position) < b.Radius + enemy.Radius + 12)
            {
                stop = true;
                if (b.AttackTimer <= 0)
                {
                    // Coup de zone autour de la cible.
                    b.AttackTimer = b.Cooldown;
                    b.SwingTime = 0.5f;
                    world.Grid.Query(enemy.Position.X, enemy.Position.Y, 100, Nearby);
                    foreach (var other in Nearby)
                        if (!other.IsDead && other is Body hit && other is not Boss && Vec2.Distance(hit.Position, enemy.Position) < 80)
                            CombatRules.ApplyDamage(world, world.Neutral, hit, b.Damage, b.Era);
                    events.Add(new ProjectileImpactEvent(world.Tick, "boss", enemy.Position.X, enemy.Position.Y, 60));
                }
            }
        }
        else if (world.TimeSeconds - b.LastDamageTime > 4) b.Hp = MathF.Min(b.MaxHp, b.Hp + b.MaxHp * t.Regen * dt); // se soigne au repos
        var delta = target - b.Position;
        float distance = delta.Length;
        float speed = stop || distance < 6 ? 0 : MathF.Min(90, distance * 3);
        b.Velocity = distance > 0 ? delta / distance * speed : Vec2.Zero;
        var next = b.Position + b.Velocity * dt;
        if (world.Terrain.IsWalkable(next, 20)) b.Position = next;
        if (speed > 5) { b.Angle = b.Velocity.Angle; b.WalkCycle += speed * dt * 0.03f; }
        else if (b.Target != null) b.Angle = (b.Target.Position - b.Position).Angle;
    }

    /// <summary>Le boss apparaît dans des ruines de la zone sûre ; sa force dépend de l'ère moyenne des clans vivants.</summary>
    private static void SpawnBoss(World world, List<GameEvent> events)
    {
        var t = world.Data.Tuning.Objectives.Boss;
        var alive = world.Armies.Where(a => a.Alive).ToList();
        int era = (int)MathF.Round(alive.Count == 0 ? 0 : (float)alive.Average(a => a.Era));
        int form = era <= 1 ? 0 : era <= 3 ? 1 : era <= 5 ? 2 : 3;
        Vec2? lair = null;
        var ruins = world.Terrain.Ruins.Where(r => !world.Zone.IsOutside(r.X, r.Y) && Vec2.Distance(new Vec2(r.X, r.Y), world.Zone.Center) < world.Zone.Radius * 0.8f).ToList();
        if (ruins.Count > 0)
        {
            var ruin = ruins[(int)(world.Random.NextDouble() * ruins.Count)];
            lair = world.DryPoint(new Vec2(ruin.X, ruin.Y), 40, 30);
        }
        lair ??= world.ZonePoint(0.6f);
        if (lair is not { } p) return;
        float hp = MathF.Round(t.Hp * (1 + era * t.HpPerEra));
        world.Boss = new Boss(world.Neutral, p, era, form, hp, t.Damage * (1 + era * t.DamagePerEra), t.Cooldown) { Angle = (float)world.Random.NextDouble() * 6.28f };
        events.Add(new BossSpawnedEvent(world.Tick, world.Boss.Name, p.X, p.Y));
    }

    /// <summary>Appelé par DeathSystem quand le boss meurt : récompense le clan qui l'a abattu.</summary>
    public static void OnBossKilled(World world, Boss boss, Army? killer, List<GameEvent> events)
    {
        var t = world.Data.Tuning.Objectives.Boss;
        world.Boss = null;
        world.BossTimer = t.Respawn * world.TimeFactor;
        int points = 0, resources = 0;
        if (killer is { Alive: true })
        {
            points = t.Points + t.PointsPerEra * boss.Era;
            resources = t.Resources;
            killer.Points += points;
            killer.Resources += resources;
            killer.Kills++;
        }
        events.Add(new BossKilledEvent(world.Tick, boss.Name, killer?.Id ?? -1, points, resources));
    }

    // ================= Portails =================

    private static void UsePortal(World world, Army army, Portal portal, List<GameEvent> events)
    {
        int maxTroops = world.Data.Tuning.Objectives.Portals.Troops;
        // Les troupes les plus en retard en priorité.
        var pool = army.Units.Where(u => !u.Dead && u.Era < Army.MaxEra).OrderBy(u => u.Era)
                             .Take(Math.Max(maxTroops, (int)MathF.Ceiling(army.Units.Count * 0.15f))).ToList();
        int n = Math.Min(maxTroops, pool.Count);
        for (int i = 0; i < n; i++)
        {
            int k = (int)(world.Random.NextDouble() * pool.Count);
            var u = pool[k];
            pool.RemoveAt(k);
            EvolutionSystem.EvolveOne(world, u, u.Era + 1);
        }
        world.Portals.Remove(portal);
        world.PortalTimer = MathF.Max(world.PortalTimer, world.Data.Tuning.Objectives.Portals.Interval);
        events.Add(new PortalUsedEvent(world.Tick, army.Id, portal.Position.X, portal.Position.Y, n));
    }

    // ================= Sanctuaires =================

    private static void UpdateShrines(World world, float dt, List<GameEvent> events)
    {
        var t = world.Data.Tuning.Objectives.Shrines;
        var present = new HashSet<Army>();
        foreach (var shrine in world.Shrines)
        {
            if (shrine.Owner is { Alive: false }) shrine.Owner = null;
            present.Clear();
            world.Grid.Query(shrine.Position.X, shrine.Position.Y, t.Radius + 20, Nearby);
            foreach (var body in Nearby)
                if (!body.IsDead && body.Owner.Alive && Vec2.Distance(body.Position, shrine.Position) < t.Radius) present.Add(body.Owner);
            if (present.Count == 1)
            {
                var army = present.First();
                if (army == shrine.Owner) continue;
                if (shrine.Capturer != army) { shrine.Capturer = army; shrine.Progress = 0; }
                shrine.Progress += dt / t.CaptureTime;
                if (shrine.Progress >= 1)
                {
                    shrine.Owner = army;
                    shrine.Progress = 0;
                    shrine.Capturer = null;
                    events.Add(new ShrineCapturedEvent(world.Tick, army.Id, shrine.Kind, shrine.Position.X, shrine.Position.Y));
                }
            }
            else shrine.Progress = MathF.Max(0, shrine.Progress - dt / t.DecayTime); // contesté ou vide : la capture retombe
        }
    }
}
