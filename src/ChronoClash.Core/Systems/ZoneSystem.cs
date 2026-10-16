using ChronoClash.Core.Events;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// La zone battle royale (port de updateZone du prototype). Chaque phase : attente, puis rétrécissement progressif
/// vers la zone suivante (choisie à l'intérieur de l'actuelle). Toutes les 0,5 s, chaque chef ou troupe HORS
/// de la zone perd une part de sa vie maximale. Phases et dégâts : data/tuning.json, section "zone".
/// En partie rapide, les délais sont réduits ; en bac à sable, la zone ne se resserre jamais.
/// </summary>
public static class ZoneSystem
{
    /// <summary>Prépare la zone de départ (elle couvre toute la carte) et la première zone cible.</summary>
    public static void Init(World world)
    {
        var z = world.Zone;
        float c = Terrain.WorldSize / 2f;
        z.Center = new Vec2(c, c);
        z.Radius = z.InitialRadius = MathF.Sqrt(2) * Terrain.WorldSize / 2 + 80;
        PickNext(world);
    }

    private static Data.ZonePhase? Phase(World world, int index)
    {
        var phases = world.Data.Tuning.Zone.Phases;
        return index < phases.Count ? phases[index] : null;
    }

    /// <summary>Durées de la phase selon le mode (partie rapide : ×0,32 ; bac à sable : jamais).</summary>
    private static (float Wait, float Shrink) Durations(World world, Data.ZonePhase phase)
    {
        if (world.Settings.Mode == GameMode.Sandbox) return (float.MaxValue, phase.Shrink);
        float f = world.Settings.Mode == GameMode.Quick ? world.Data.Tuning.QuickMode.ZoneFactor : 1;
        return (MathF.Round(phase.Wait * f), MathF.Round(phase.Shrink * f));
    }

    /// <summary>Choisit le centre de la prochaine zone, entièrement à l'intérieur de l'actuelle.</summary>
    private static void PickNext(World world)
    {
        var z = world.Zone;
        var phase = Phase(world, z.Phase);
        if (phase == null) { z.NextCenter = null; return; }
        float nr = phase.Radius, r = z.Radius;
        Vec2 best = z.Center;
        for (int k = 0; k < 80; k++)
        {
            float a = (float)world.Random.NextDouble() * MathF.PI * 2, d = (float)world.Random.NextDouble() * MathF.Max(0, r - nr) * 0.9f;
            float x = MathUtil.Clamp(z.Center.X + MathF.Cos(a) * d, nr * 0.3f + 200, Terrain.WorldSize - nr * 0.3f - 200);
            float y = MathUtil.Clamp(z.Center.Y + MathF.Sin(a) * d, nr * 0.3f + 200, Terrain.WorldSize - nr * 0.3f - 200);
            var p = new Vec2(x, y);
            if (Vec2.Distance(p, z.Center) + nr > r + 1 && r > 0) continue;
            best = p;
            if (!world.Terrain.IsWater(x, y)) break; // on préfère un centre sur la terre ferme
        }
        z.NextCenter = best;
        z.NextRadius = nr;
    }

    public static void Update(World world, float dt, List<GameEvent> events)
    {
        var z = world.Zone;
        z.Timer += dt;
        var phase = Phase(world, z.Phase);
        if (phase != null)
        {
            var (wait, shrink) = Durations(world, phase);
            if (z.Stage == ZoneStage.Waiting && z.Timer >= wait)
            {
                z.Stage = ZoneStage.Shrinking;
                z.Timer = 0;
                z.FromCenter = z.Center;
                z.FromRadius = z.Radius;
                z.DamagePercent = phase.Damage;
                events.Add(new ZoneShrinkingEvent(world.Tick, z.Phase));
            }
            else if (z.Stage == ZoneStage.Shrinking && z.NextCenter is { } next)
            {
                float q = MathF.Min(1, z.Timer / MathF.Max(0.01f, shrink));
                z.Center = Vec2.Lerp(z.FromCenter, next, q);
                z.Radius = z.FromRadius + (z.NextRadius - z.FromRadius) * q;
                if (q >= 1)
                {
                    z.Phase++;
                    z.Stage = ZoneStage.Waiting;
                    z.Timer = 0;
                    PickNext(world);
                }
            }
        }
        else
        {
            z.Stage = ZoneStage.Final;
            z.Radius = 0;
            z.DamagePercent = 0.3f;
        }

        // Dégâts hors zone, toutes les 0,5 s : seulement les corps individuellement dehors.
        z.DamageTimer -= dt;
        if (z.DamageTimer > 0 || z.DamagePercent <= 0) return;
        float tick = world.Data.Tuning.Zone.DamageTick;
        z.DamageTimer = tick;
        foreach (var army in world.Armies)
        {
            if (!army.Alive) continue;
            var g = army.General;
            if (!g.Dead && z.IsOutside(g.Position)) CombatRules.ApplyDamage(world, null, g, g.MaxHp * z.DamagePercent * tick, 0);
            foreach (var u in army.Units)
                if (!u.Dead && z.IsOutside(u.Position)) CombatRules.ApplyDamage(world, null, u, u.MaxHp * z.DamagePercent * tick, 0);
        }
    }

    /// <summary>Temps restant avant la fin de l'étape en cours (attente ou rétrécissement), ou -1 s'il n'y en a pas.</summary>
    public static float TimeLeft(World world)
    {
        var z = world.Zone;
        var phase = Phase(world, z.Phase);
        if (phase == null) return -1;
        var (wait, shrink) = Durations(world, phase);
        if (wait > 1e8f) return -1;
        return MathF.Max(0, (z.Stage == ZoneStage.Waiting ? wait : shrink) - z.Timer);
    }

    /// <summary>
    /// Où un bot doit aller pour être en sécurité, ou null s'il est déjà bien à l'intérieur.
    /// Il anticipe : peu avant le rétrécissement, il rejoint la PROCHAINE zone.
    /// </summary>
    public static Vec2? SafeSpot(World world, Vec2 from)
    {
        var z = world.Zone;
        bool inside = Vec2.Distance(from, z.Center) < z.Radius * 0.9f;
        var phase = Phase(world, z.Phase);
        float left = TimeLeft(world);
        bool soon = z.NextCenter.HasValue && (z.Stage == ZoneStage.Shrinking || (phase != null && left >= 0 && left < 55));
        bool outsideNext = z.NextCenter is { } nc && Vec2.Distance(from, nc) > z.NextRadius * 0.85f;
        if (inside && !(soon && outsideNext)) return null;
        var center = soon && z.NextCenter.HasValue ? z.NextCenter.Value : z.Center;
        float radius = soon && z.NextCenter.HasValue ? z.NextRadius : MathF.Max(z.Radius, 60);
        var d = from - center;
        float dist = MathF.Max(1, d.Length), rr = MathF.Min(dist, radius * 0.45f);
        for (int k = 0; k < 8; k++)
        {
            float jitter = k * 60;
            var p = center + d / dist * rr + new Vec2(((float)world.Random.NextDouble() - 0.5f) * jitter, ((float)world.Random.NextDouble() - 0.5f) * jitter);
            if (world.Terrain.IsWalkable(p, 16)) return p;
        }
        return center;
    }
}
