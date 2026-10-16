using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Entities;

/// <summary>Type de bulle : une troupe de chaque type, ou une étoile (points ★).</summary>
public enum BubbleKind : byte { Warrior = 0, Ranged = 1, Tank = 2, Star = 3 }

/// <summary>Une bulle à ramasser sur la carte. Il suffit qu'un chef ou une troupe la touche.</summary>
public sealed class Bubble
{
    public Bubble(int id, BubbleKind kind, Vec2 position, float radius, float phase)
    {
        Id = id;
        Kind = kind;
        Position = position;
        Radius = radius;
        Phase = phase;
    }

    public int Id { get; }
    public BubbleKind Kind { get; }
    public Vec2 Position { get; }
    public float Radius { get; }
    /// <summary>Décalage d'animation (pour que les bulles ne flottent pas toutes en même temps).</summary>
    public float Phase { get; }
    /// <summary>Âge en secondes (animation d'apparition).</summary>
    public float Age;
}
