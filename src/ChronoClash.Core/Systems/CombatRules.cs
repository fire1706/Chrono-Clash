using ChronoClash.Core.Commands;
using ChronoClash.Core.Data;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Systems;

/// <summary>
/// Règles de combat partagées par tous les systèmes : infliger des dégâts, tirer un projectile,
/// multiplicateurs (vétéran, cri de guerre, colline), multiplicateur d'outsider.
/// C'est le SEUL endroit qui retire de la vie : toute nouvelle source de dégâts doit passer par <see cref="ApplyDamage"/>.
/// </summary>
public static class CombatRules
{
    /// <summary>
    /// Inflige des dégâts. Les dégâts d'un attaquant sont multipliés par combat.damageMultiplier (tuning.json).
    /// Si la victime meurt, elle est mise dans la file des morts traitée à la fin du tick par <see cref="DeathSystem"/>.
    /// </summary>
    /// <param name="attacker">Clan de l'attaquant, ou null (zone, événement).</param>
    /// <param name="sourceEra">Ère de l'attaquant (pour le multiplicateur d'outsider).</param>
    public static void ApplyDamage(World world, Army? attacker, Body target, float amount, int sourceEra)
    {
        if (target.Dead) return;
        if (attacker != null) amount *= world.Data.Tuning.Combat.DamageMultiplier;
        target.Hp -= amount;
        target.LastDamageTime = world.TimeSeconds;
        target.HitFlash = 0.12f;
        if (attacker != null) target.Owner.LastHitBy = attacker;
        if (target.Hp <= 0)
        {
            target.Hp = 0;
            target.Dead = true;
            world.PendingDeaths.Add(new PendingDeath(target, attacker, sourceEra));
        }
    }

    /// <summary>
    /// Multiplicateur d'outsider : tuer un ennemi d'une ère plus avancée rapporte ×2 par ère d'écart, sans limite.
    /// C'est ce qui permet à un clan en retard de remonter.
    /// </summary>
    public static int UnderdogMultiplier(int killerEra, int victimEra) => 1 << Math.Clamp(victimEra - killerEra, 0, 20);

    /// <summary>Multiplicateur de dégâts d'une troupe : +10 % par chevron de vétéran, ×1,3 sous le cri de guerre.</summary>
    public static float DamageMultiplier(World world, Unit u)
    {
        var t = world.Data.Tuning;
        return (1 + t.Veterans.BonusPerLevel * u.Veteran) * (u.WarCryTime > 0 ? t.Tactics.WarCry.Damage : 1);
    }

    /// <summary>Portée réelle d'un tireur : +25 % sur une colline.</summary>
    public static float RangeMultiplier(World world, Body b)
        => world.Terrain.IsHill(b.Position.X, b.Position.Y) ? world.Data.Tuning.Combat.HillRangeMultiplier : 1f;

    /// <summary>
    /// Tire un projectile vers une cible, en visant là où elle sera à l'arrivée (tir anticipé),
    /// avec une petite dispersion (plus grande pour les armes à dégâts de zone).
    /// </summary>
    public static void Shoot(World world, Body shooter, Body target, string kind, float damage, float splash, int era, TroopKind? sourceKind)
    {
        float speed = world.Data.Tuning.Combat.SpeedOf(kind);
        float travel = Vec2.Distance(shooter.Position, target.Position) / speed;
        var lead = target.Position + target.Velocity * travel;
        float spread = splash > 0 ? 24 : 8;
        var aim = lead + new Vec2(((float)world.Random.NextDouble() - 0.5f) * spread, ((float)world.Random.NextDouble() - 0.5f) * spread);
        // Petit temps en plus selon l'arme (le rocher monte haut, la balle est quasi instantanée).
        float extra = kind == "rock" ? 0.35f : kind == "bullet" ? 0.03f : 0.12f;
        world.Projectiles.Add(new Projectile
        {
            Kind = kind, From = shooter.Position, To = aim, FlightTime = Vec2.Distance(shooter.Position, aim) / speed + extra,
            Target = target, Owner = shooter.Owner, Damage = damage, SourceEra = era, Splash = splash, SourceKind = sourceKind,
        });
    }
}

/// <summary>Une mort à traiter en fin de tick (récompenses, succession, élimination).</summary>
public sealed record PendingDeath(Body Victim, Army? Killer, int SourceEra);
