using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Le chef d'un clan. C'est lui que le joueur dirige ; l'armée se place autour de lui (étape M2).
/// Ne contient que des données : il est déplacé par <see cref="Systems.GeneralMovementSystem"/>.
/// </summary>
public sealed class General : IBody
{
    /// <summary>Rayon de collision du chef, en pixels.</summary>
    public const float Radius = 14;

    // ---- Position et mouvement ----
    public Vec2 Position { get; set; }
    /// <summary>Position au tick précédent : l'affichage glisse entre les deux pour rester fluide.</summary>
    public Vec2 PreviousPosition;
    public Vec2 Velocity;
    /// <summary>Direction du regard en radians (0 = vers la droite). C'est aussi « l'avant » de la future formation.</summary>
    public float Angle = -MathF.PI / 2;
    /// <summary>Compteur d'animation de marche (avance avec la vitesse).</summary>
    public float WalkCycle;

    // ---- Ordres reçus ----
    /// <summary>Direction demandée au clavier (zéro si aucune touche). Prioritaire sur le chemin.</summary>
    public Vec2 InputDirection;
    /// <summary>Étapes restantes du chemin demandé au clic droit.</summary>
    public List<Vec2> Path { get; } = new();
    /// <summary>Le joueur maintient la touche de sprint.</summary>
    public bool WantsSprint;

    // ---- Endurance (sprint) ----
    public float Stamina = 100;
    /// <summary>Délai avant que l'endurance recommence à remonter.</summary>
    public float StaminaRestTimer;
    /// <summary>Épuisé : plus de sprint tant que l'endurance n'est pas remontée.</summary>
    public bool Exhausted;
    /// <summary>En train de sprinter à ce tick.</summary>
    public bool Sprinting;

    // ---- Détection de blocage ----
    public float StuckTime;
    public Vec2 LastPosition;

    // ---- Vie (utilisée à partir de l'étape M3) ----
    public float Hp = 240;
    public float MaxHp = 240;
    public bool Dead;

    /// <summary>Le clan de ce chef (renseigné par le constructeur de <see cref="Army"/>).</summary>
    public Army Owner { get; internal set; } = null!;
    public float BodyRadius => Radius;
    public bool IsDead => Dead;

    public General(Vec2 position)
    {
        Position = PreviousPosition = LastPosition = position;
    }
}
