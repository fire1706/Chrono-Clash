using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Net;

/// <summary>
/// Applique un snapshot de l'hôte à la copie locale du monde d'un client.
/// Pour chaque chef ou troupe, l'ancienne position devient « PreviousPosition » : l'affichage glisse
/// de l'une à l'autre jusqu'au snapshot suivant (interpolation), ce qui rend le mouvement fluide.
/// Les troupes qui ne figurent plus dans le snapshot (mortes ou sorties de la vue) sont retirées.
/// L'ordre de lecture doit être EXACTEMENT celui de <see cref="SnapshotWriter"/>.
/// </summary>
public static class SnapshotReader
{
    public static float DQ(ushort q) => q / 65535f * Terrain.WorldSize;
    private static float DAngle(byte b) => b / 256f * MathF.PI * 2;

    public static void Apply(World world, byte[] data)
    {
        using var r = new BinaryReader(new MemoryStream(data));
        if ((MessageType)r.ReadByte() != MessageType.Snapshot) return;
        world.Tick = r.ReadInt64();
        world.IsOver = r.ReadBoolean();
        int winner = r.ReadSByte();
        world.Winner = winner >= 0 && winner < world.Armies.Count ? world.Armies[winner] : null;

        var z = world.Zone;
        z.Center = new Vec2(r.ReadSingle(), r.ReadSingle()); z.Radius = r.ReadSingle(); z.InitialRadius = r.ReadSingle();
        z.Phase = r.ReadByte(); z.Stage = (ZoneStage)r.ReadByte(); z.Timer = r.ReadSingle(); z.DamagePercent = r.ReadSingle();
        if (r.ReadBoolean()) { z.NextCenter = new Vec2(r.ReadSingle(), r.ReadSingle()); z.NextRadius = r.ReadSingle(); }
        else z.NextCenter = null;
        world.StormTime = r.ReadSingle();

        int armies = r.ReadByte();
        for (int i = 0; i < armies && i < world.Armies.Count; i++) ReadArmy(world, world.Armies[i], r);

        world.Projectiles.Clear();
        int shots = r.ReadUInt16();
        for (int i = 0; i < shots; i++)
        {
            string kind = SnapshotWriter.ProjectileKinds[Math.Min(r.ReadByte(), SnapshotWriter.ProjectileKinds.Length - 1)];
            var from = new Vec2(DQ(r.ReadUInt16()), DQ(r.ReadUInt16()));
            var to = new Vec2(DQ(r.ReadUInt16()), DQ(r.ReadUInt16()));
            float elapsed = r.ReadSingle(), flight = r.ReadSingle(), splash = r.ReadSingle();
            // La cible et le tireur ne servent qu'à l'hôte : on met des valeurs neutres.
            world.Projectiles.Add(new Projectile
            {
                Kind = kind, From = from, To = to, FlightTime = flight, Target = world.Neutral.General, Owner = world.Neutral,
                Damage = 0, SourceEra = 0, Splash = splash, Elapsed = elapsed,
            });
        }

        world.Crate = r.ReadBoolean() ? new Crate(new Vec2(r.ReadSingle(), r.ReadSingle()), 0) { TimeToLand = r.ReadSingle() } : null;
        bool bossAlive = r.ReadBoolean(), bossVisible = r.ReadBoolean();
        if (bossAlive)
        {
            var pos = new Vec2(r.ReadSingle(), r.ReadSingle());
            if (bossVisible)
            {
                float angle = r.ReadSingle(), walk = r.ReadSingle(), hp = r.ReadSingle(), max = r.ReadSingle();
                int form = r.ReadByte(), era = r.ReadByte();
                float flash = r.ReadByte() / 255f * 0.12f;
                var vel = new Vec2(r.ReadSingle(), r.ReadSingle());
                if (world.Boss == null || world.Boss.Form != form) world.Boss = new Boss(world.Neutral, pos, era, form, max, 0, 1);
                var b = world.Boss;
                b.PreviousPosition = b.Position; b.Position = pos;
                b.Angle = angle; b.WalkCycle = walk; b.Hp = hp; b.MaxHp = max; b.HitFlash = flash; b.Velocity = vel;
            }
            else if (world.Boss == null) world.Boss = new Boss(world.Neutral, pos, 0, 0, 1, 0, 1);
            else world.Boss.Position = world.Boss.PreviousPosition = pos;
        }
        else world.Boss = null;

        world.Portals.Clear();
        int portals = r.ReadByte();
        for (int i = 0; i < portals; i++) world.Portals.Add(new Portal(r.ReadInt32(), new Vec2(r.ReadSingle(), r.ReadSingle()), r.ReadSingle()));

        world.Shrines.Clear();
        int shrines = r.ReadByte();
        for (int i = 0; i < shrines; i++)
        {
            var s = new Shrine(r.ReadInt32(), (ShrineKind)r.ReadByte(), new Vec2(r.ReadSingle(), r.ReadSingle()));
            int owner = r.ReadSByte(), capturer = r.ReadSByte();
            s.Owner = owner >= 0 && owner < world.Armies.Count ? world.Armies[owner] : null;
            s.Capturer = capturer >= 0 && capturer < world.Armies.Count ? world.Armies[capturer] : null;
            s.Progress = r.ReadSingle();
            world.Shrines.Add(s);
        }

        world.Meteors.Clear();
        int meteors = r.ReadUInt16();
        for (int i = 0; i < meteors; i++) world.Meteors.Add(new Meteor(new Vec2(r.ReadSingle(), r.ReadSingle()), r.ReadSingle()));
    }

    private static void ReadArmy(World world, Army a, BinaryReader r)
    {
        a.Alive = r.ReadBoolean();
        a.Points = r.ReadInt32();
        a.Resources = r.ReadInt32();
        a.Era = r.ReadByte();
        int unitCount = r.ReadUInt16();
        a.Placement = r.ReadByte();
        a.Kills = r.ReadUInt16();
        int evoTarget = r.ReadSByte();
        float evoElapsed = r.ReadSingle();
        a.Evolution = evoTarget >= 0 ? new EraEvolution(evoTarget, new List<Body>()) { Elapsed = evoElapsed } : null;
        a.IsBot = r.ReadBoolean();
        a.FormationIndex = r.ReadByte();
        a.FormationAngle = r.ReadSingle();
        for (int i = 0; i < 4; i++) a.TacticCooldowns[i] = r.ReadSingle();
        a.Bonus = new ArmyBonus(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        a.ReportedUnitCount = unitCount;

        var g = a.General;
        if (r.ReadBoolean())
        {
            var pos = new Vec2(r.ReadSingle(), r.ReadSingle());
            g.Velocity = new Vec2(r.ReadSingle(), r.ReadSingle());
            g.Angle = r.ReadSingle(); g.WalkCycle = r.ReadSingle();
            g.Hp = r.ReadSingle(); g.MaxHp = r.ReadSingle();
            g.Era = r.ReadByte(); g.Dead = r.ReadBoolean(); g.Promoted = r.ReadBoolean(); g.HeirKind = (TroopKind)r.ReadByte();
            g.Sprinting = r.ReadBoolean(); g.Stamina = r.ReadSingle(); g.Exhausted = r.ReadBoolean();
            g.HitFlash = r.ReadByte() / 255f * 0.12f; g.EvolveFlash = r.ReadByte() / 255f * 0.8f;
            int path = r.ReadUInt16();
            g.Path.Clear();
            for (int i = 0; i < path; i++) g.Path.Add(new Vec2(r.ReadSingle(), r.ReadSingle()));
            // Un grand saut (succession, retour près du chef…) : pas d'interpolation, on téléporte.
            g.PreviousPosition = Vec2.Distance(g.Position, pos) > 200 ? pos : g.Position;
            g.Position = pos;
            a.ChiefVisible = true;
        }
        else a.ChiefVisible = false;

        int units = r.ReadUInt16();
        var byId = new Dictionary<int, Unit>(a.Units.Count);
        foreach (var u in a.Units) byId[u.Id] = u;
        var fresh = new List<Unit>(units);
        for (int i = 0; i < units; i++)
        {
            int id = r.ReadInt32();
            int kindEra = r.ReadByte();
            var kind = (TroopKind)(kindEra >> 4);
            int era = kindEra & 15;
            var pos = new Vec2(DQ(r.ReadUInt16()), DQ(r.ReadUInt16()));
            float angle = DAngle(r.ReadByte());
            float hpRatio = r.ReadByte() / 255f;
            float maxHp = r.ReadSingle();
            int flags = r.ReadByte();
            float walk = DAngle(r.ReadByte());
            if (!byId.TryGetValue(id, out var u))
                u = new Unit(id, a, kind, era, pos, world.Data.Troop(kind).Radius, maxHp) { Angle = angle };
            u.PreviousPosition = Vec2.Distance(u.Position, pos) > 200 ? pos : u.Position;
            u.Position = pos;
            u.Era = era;
            u.Angle = angle;
            u.MaxHp = maxHp;
            u.Hp = hpRatio * maxHp;
            u.ChargeTime = (flags & 1) != 0 ? 1 : 0;
            u.WarCryTime = (flags & 2) != 0 ? 1 : 0;
            u.Velocity = (flags & 4) != 0 ? Vec2.FromAngle(angle) * 60 : Vec2.Zero; // seulement pour l'animation de marche
            u.Veteran = (flags >> 3) & 3;
            u.HitFlash = (flags & 32) != 0 ? 0.12f : 0;
            u.EvolveFlash = (flags & 64) != 0 ? 0.8f : 0;
            u.WalkCycle = walk;
            u.Age = 1;
            fresh.Add(u);
        }
        a.Units.Clear();
        a.Units.AddRange(fresh);
    }
}
