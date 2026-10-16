using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Net;

/// <summary>
/// Écrit un « snapshot » : l'état du monde tel que le voit UN clan, envoyé 10 fois par seconde à son joueur.
/// Filtré par le brouillard de guerre : les troupes, chefs, projectiles et boss hors de vue ne sont pas envoyés
/// (moins de réseau, et impossible de tricher en affichant ce qu'on n'a pas reçu).
///
/// Format (l'ordre doit être EXACTEMENT celui de SnapshotReader) :
/// en-tête → zone → clans (résumé + chef + troupes visibles) → projectiles → largage → boss → portails → sanctuaires → météores.
/// Les positions sont compressées sur 2 octets (précision ≈ 0,2 px), les angles et la vie sur 1 octet.
/// </summary>
public static class SnapshotWriter
{
    /// <summary>Noms des projectiles, envoyés par leur numéro dans cette liste.</summary>
    public static readonly string[] ProjectileKinds = { "stone", "arrow", "rock", "ball", "bullet", "rocket", "shell", "plasma", "boss" };

    /// <summary>Position du monde → nombre de 0 à 65 535.</summary>
    public static ushort Q(float v) => (ushort)Math.Clamp(v / Terrain.WorldSize * 65535f, 0, 65535);
    /// <summary>Angle en radians → nombre de 0 à 255.</summary>
    public static byte QAngle(float a)
    {
        float t = a / (MathF.PI * 2);
        t -= MathF.Floor(t);
        return (byte)Math.Clamp((int)(t * 256), 0, 255);
    }
    /// <summary>Valeur entre 0 et 1 → nombre de 0 à 255.</summary>
    public static byte Q01(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255), 0, 255);

    public static byte[] Write(World world, Army viewer)
    {
        using var stream = new MemoryStream(4096);
        using var w = new BinaryWriter(stream);
        bool fog = world.Settings.FogOfWar;
        bool Sees(Mathematics.Vec2 p) => !fog || !viewer.Alive || world.IsVisibleTo(viewer, p);

        w.Write((byte)MessageType.Snapshot);
        w.Write(world.Tick);
        w.Write(world.IsOver);
        w.Write((sbyte)(world.Winner?.Id ?? -1));

        // ---- Zone et météo ----
        var z = world.Zone;
        w.Write(z.Center.X); w.Write(z.Center.Y); w.Write(z.Radius); w.Write(z.InitialRadius);
        w.Write((byte)z.Phase); w.Write((byte)z.Stage); w.Write(z.Timer); w.Write(z.DamagePercent);
        w.Write(z.NextCenter.HasValue);
        if (z.NextCenter is { } nc) { w.Write(nc.X); w.Write(nc.Y); w.Write(z.NextRadius); }
        w.Write(world.StormTime);

        // ---- Clans ----
        w.Write((byte)world.Armies.Count);
        foreach (var a in world.Armies)
        {
            bool mine = a == viewer;
            w.Write(a.Alive);
            w.Write(a.Points);
            w.Write(mine ? a.Resources : 0);
            w.Write((byte)a.Era);
            w.Write((ushort)a.Units.Count);
            w.Write((byte)a.Placement);
            w.Write((ushort)a.Kills);
            w.Write((sbyte)(a.Evolution?.TargetEra ?? -1));
            w.Write(a.Evolution?.Elapsed ?? 0);
            w.Write(a.IsBot);
            w.Write((byte)a.FormationIndex);
            w.Write(a.FormationAngle);
            for (int i = 0; i < 4; i++) w.Write(mine ? a.TacticCooldowns[i] : 0f);
            w.Write(a.Bonus.Speed); w.Write(a.Bonus.Regen); w.Write(a.Bonus.Damage);

            // Chef (toujours le nôtre ; les autres seulement s'ils sont en vue).
            var g = a.General;
            bool showChief = mine || Sees(g.Position);
            w.Write(showChief);
            if (showChief)
            {
                w.Write(g.Position.X); w.Write(g.Position.Y);
                w.Write(g.Velocity.X); w.Write(g.Velocity.Y);
                w.Write(g.Angle); w.Write(g.WalkCycle);
                w.Write(g.Hp); w.Write(g.MaxHp);
                w.Write((byte)g.Era); w.Write(g.Dead); w.Write(g.Promoted); w.Write((byte)g.HeirKind);
                w.Write(g.Sprinting); w.Write(g.Stamina); w.Write(g.Exhausted);
                w.Write(Q01(g.HitFlash / 0.12f)); w.Write(Q01(g.EvolveFlash / 0.8f));
                w.Write((ushort)(mine ? g.Path.Count : 0));
                if (mine) foreach (var step in g.Path) { w.Write(step.X); w.Write(step.Y); }
            }

            // Troupes visibles.
            var units = mine || !fog ? a.Units.Where(u => !u.Dead).ToList() : a.Units.Where(u => !u.Dead && Sees(u.Position)).ToList();
            w.Write((ushort)units.Count);
            foreach (var u in units)
            {
                w.Write(u.Id);
                w.Write((byte)(((int)u.Kind << 4) | u.Era));
                w.Write(Q(u.Position.X)); w.Write(Q(u.Position.Y));
                w.Write(QAngle(u.Angle));
                w.Write(Q01(u.Hp / MathF.Max(1, u.MaxHp)));
                w.Write(u.MaxHp);
                int flags = (u.ChargeTime > 0 ? 1 : 0) | (u.WarCryTime > 0 ? 2 : 0) | (u.Velocity.LengthSquared > 144 ? 4 : 0)
                          | (Math.Min(u.Veteran, 3) << 3) | (u.HitFlash > 0 ? 32 : 0) | (u.EvolveFlash > 0 ? 64 : 0);
                w.Write((byte)flags);
                w.Write(QAngle(u.WalkCycle));
            }
        }

        // ---- Projectiles visibles ----
        var shots = world.Projectiles.Where(p => Sees(p.To) || Sees(p.From)).ToList();
        w.Write((ushort)shots.Count);
        foreach (var p in shots)
        {
            w.Write((byte)Math.Max(0, Array.IndexOf(ProjectileKinds, p.Kind)));
            w.Write(Q(p.From.X)); w.Write(Q(p.From.Y)); w.Write(Q(p.To.X)); w.Write(Q(p.To.Y));
            w.Write(p.Elapsed); w.Write(p.FlightTime); w.Write(p.Splash);
        }

        // ---- Carte vivante ----
        w.Write(world.Crate != null);
        if (world.Crate is { } crate) { w.Write(crate.Position.X); w.Write(crate.Position.Y); w.Write(crate.TimeToLand); }
        bool showBoss = world.Boss is { Dead: false } b0 && Sees(b0.Position);
        w.Write(world.Boss is { Dead: false });
        w.Write(showBoss);
        if (world.Boss is { Dead: false } boss)
        {
            w.Write(boss.Position.X); w.Write(boss.Position.Y);
            if (showBoss)
            {
                w.Write(boss.Angle); w.Write(boss.WalkCycle); w.Write(boss.Hp); w.Write(boss.MaxHp);
                w.Write((byte)boss.Form); w.Write((byte)boss.Era); w.Write(Q01(boss.HitFlash / 0.12f));
                w.Write(boss.Velocity.X); w.Write(boss.Velocity.Y);
            }
        }
        w.Write((byte)world.Portals.Count);
        foreach (var p in world.Portals) { w.Write(p.Id); w.Write(p.Position.X); w.Write(p.Position.Y); w.Write(p.Phase); }
        w.Write((byte)world.Shrines.Count);
        foreach (var s in world.Shrines)
        {
            w.Write(s.Id); w.Write((byte)s.Kind); w.Write(s.Position.X); w.Write(s.Position.Y);
            w.Write((sbyte)(s.Owner?.Id ?? -1)); w.Write((sbyte)(s.Capturer?.Id ?? -1)); w.Write(s.Progress);
        }
        w.Write((ushort)world.Meteors.Count);
        foreach (var m in world.Meteors) { w.Write(m.Position.X); w.Write(m.Position.Y); w.Write(m.TimeToImpact); }

        w.Flush();
        return stream.ToArray();
    }
}
