using ChronoClash.Core.Commands;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Un projectile en vol (pierre, flèche, rocher, balle, roquette, obus, plasma).
/// Il part de <see cref="From"/> vers <see cref="To"/> (le point où sera la cible, anticipé) et arrive au bout de
/// <see cref="FlightTime"/> secondes. À l'arrivée : dégâts sur la cible si elle est encore là, ou dégâts de zone.
/// </summary>
public sealed class Projectile
{
    public required string Kind { get; init; }
    public required Vec2 From { get; init; }
    public required Vec2 To { get; init; }
    public required float FlightTime { get; init; }
    public required Body Target { get; init; }
    public required Army Owner { get; init; }
    public required float Damage { get; init; }
    /// <summary>Ère du tireur (pour le multiplicateur d'outsider).</summary>
    public required int SourceEra { get; init; }
    /// <summary>Rayon des dégâts de zone (0 = touche seulement la cible).</summary>
    public float Splash { get; init; }
    /// <summary>Type du tireur (null = un chef), pour les statistiques.</summary>
    public TroopKind? SourceKind { get; init; }

    public float Elapsed { get; set; }

    /// <summary>Hauteur de la trajectoire en cloche (pixels) : pierres, flèches et rochers montent puis retombent.</summary>
    public float ArcHeight => Kind switch { "stone" => 22, "arrow" => 22, "rock" => 90, _ => 0 };
}
