namespace ChronoClash.Core.Map;

/// <summary>
/// Obstacle infranchissable : un rocher, ou un pilier de ruine.
/// <see cref="Outline"/> contient les coins du contour (x0, y0, x1, y1…) relatifs au centre, pour l'affichage.
/// </summary>
public sealed class Rock
{
    public float X { get; init; }
    public float Y { get; init; }
    /// <summary>Rayon utilisé pour les collisions.</summary>
    public float Radius { get; init; }
    public float[] Outline { get; init; } = Array.Empty<float>();
    /// <summary>Vrai pour un pilier de ruine (dessiné comme un bloc de pierre taillée).</summary>
    public bool IsRuinPillar { get; init; }
    /// <summary>Orientation du pilier (radians).</summary>
    public float Angle { get; init; }
    /// <summary>Pilier cassé (dessiné plus court).</summary>
    public bool Broken { get; init; }
}

/// <summary>Site de ruines anciennes : un cercle de piliers. Les étoiles y apparaissent souvent (étape M4).</summary>
public sealed record Ruin(float X, float Y, float Radius);
