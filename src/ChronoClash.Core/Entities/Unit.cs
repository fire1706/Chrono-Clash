using ChronoClash.Core.Commands;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>
/// Une troupe (guerrier, troupe à distance ou tank). Objet volontairement léger : il peut y en avoir
/// des centaines par armée. Elle est déplacée par <see cref="Systems.UnitMovementSystem"/> vers sa place
/// dans la formation, calculée par <see cref="Systems.FormationSystem"/>.
/// </summary>
public sealed class Unit : IBody
{
    public Unit(int id, Army owner, TroopKind kind, int era, Vec2 position, float radius, float hp)
    {
        Id = id;
        Owner = owner;
        Kind = kind;
        Era = era;
        Position = PreviousPosition = LastPosition = position;
        Radius = radius;
        Hp = MaxHp = hp;
    }

    /// <summary>Identifiant unique dans la partie (sert aussi à trier les voisins sans double comptage).</summary>
    public int Id { get; }
    public Army Owner { get; }
    public TroopKind Kind { get; }
    /// <summary>Ère de CETTE troupe (pendant une évolution, une armée mélange deux ères).</summary>
    public int Era { get; set; }
    public float Radius { get; }

    // ---- Mouvement ----
    public Vec2 Position { get; set; }
    public Vec2 PreviousPosition;
    public Vec2 Velocity;
    public float Angle;
    public float WalkCycle;

    /// <summary>Place dans la formation, relative au chef : X = vers l'avant, Y = vers la droite (pixels).</summary>
    public Vec2 Slot;
    /// <summary>Chemin vers sa place quand un obstacle la sépare de son chef (sinon null).</summary>
    public List<Vec2>? Path;
    public float StuckTime;
    /// <summary>Temps passé bloquée loin de son chef (au-delà de 5 s, elle le rejoint d'un coup).</summary>
    public float LostTime;
    public Vec2 LastPosition;
    /// <summary>Âge en secondes (petite animation d'apparition).</summary>
    public float Age;

    // ---- Vie (combat à l'étape M3) ----
    public float Hp;
    public float MaxHp;
    public bool Dead;

    public float BodyRadius => Radius;
    public bool IsDead => Dead;
}
