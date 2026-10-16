using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Caisse de ravitaillement (largage) : annoncée 20 s à l'avance, elle tombe du ciel puis le premier clan
/// qui la touche gagne des ◆, des ★ et 4 troupes.
/// </summary>
public sealed class Crate
{
    public Crate(Vec2 position, float warning)
    {
        Position = position;
        TimeToLand = warning;
    }

    public Vec2 Position { get; }
    /// <summary>Secondes avant l'atterrissage (0 = posée, on peut la ramasser).</summary>
    public float TimeToLand { get; set; }
    public bool Landed => TimeToLand <= 0;
}

/// <summary>Portail temporel : le premier clan qui le traverse fait passer jusqu'à 8 troupes à l'ère suivante.</summary>
public sealed class Portal
{
    public Portal(int id, Vec2 position, float phase)
    {
        Id = id;
        Position = position;
        Phase = phase;
    }

    public int Id { get; }
    public Vec2 Position { get; }
    /// <summary>Rotation du tourbillon (animation).</summary>
    public float Phase { get; set; }
}

/// <summary>Type de sanctuaire et son bonus pour le clan qui le tient.</summary>
public enum ShrineKind : byte
{
    /// <summary>+15 % de vitesse.</summary>
    Wind,
    /// <summary>Soin de 1,5 % de la vie par seconde.</summary>
    Life,
    /// <summary>+12 % de dégâts.</summary>
    Power,
}

/// <summary>Sanctuaire : un clan seul dans le cercle pendant 4 s le capture et garde son bonus tant qu'il le tient.</summary>
public sealed class Shrine
{
    public Shrine(int id, ShrineKind kind, Vec2 position)
    {
        Id = id;
        Kind = kind;
        Position = position;
    }

    public int Id { get; }
    public ShrineKind Kind { get; }
    public Vec2 Position { get; }
    public Army? Owner { get; set; }
    /// <summary>Clan en train de capturer, et avancement (0 à 1).</summary>
    public Army? Capturer { get; set; }
    public float Progress { get; set; }
}

/// <summary>Météore annoncé par un cercle rouge, puis qui s'écrase (dégâts de zone, parfois une bulle rare).</summary>
public sealed class Meteor
{
    public Meteor(Vec2 position, float delay)
    {
        Position = position;
        TimeToImpact = delay;
    }

    public Vec2 Position { get; }
    /// <summary>Secondes avant l'impact. L'avertissement s'affiche pendant les 1,5 dernières secondes.</summary>
    public float TimeToImpact { get; set; }
}

/// <summary>Bonus d'un clan grâce aux sanctuaires qu'il tient.</summary>
public readonly record struct ArmyBonus(float Speed, float Regen, float Damage)
{
    public static readonly ArmyBonus None = new(1, 0, 1);
}
