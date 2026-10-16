namespace ChronoClash.Core.Mathematics;

/// <summary>
/// Vecteur 2D (position, vitesse, direction) du cœur du jeu.
/// On n'utilise PAS le Vector2 de Godot ici, pour que le cœur reste indépendant du moteur.
/// Côté Godot, la conversion se fait dans game/scripts/Presentation/CoreConvert.cs.
/// </summary>
public readonly record struct Vec2(float X, float Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    /// <summary>Longueur du vecteur.</summary>
    public float Length => MathF.Sqrt(X * X + Y * Y);

    /// <summary>Longueur au carré : plus rapide quand on veut juste comparer des distances.</summary>
    public float LengthSquared => X * X + Y * Y;

    /// <summary>Même direction, longueur 1 (ou zéro si le vecteur est nul).</summary>
    public Vec2 Normalized()
    {
        float l = Length;
        return l > 1e-6f ? new Vec2(X / l, Y / l) : Zero;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator *(Vec2 a, float k) => new(a.X * k, a.Y * k);
    public static Vec2 operator /(Vec2 a, float k) => new(a.X / k, a.Y / k);

    public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
    public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => a + (b - a) * t;

    /// <summary>Vecteur de longueur 1 pointant dans la direction de l'angle (en radians, 0 = vers la droite).</summary>
    public static Vec2 FromAngle(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    /// <summary>Angle de ce vecteur en radians.</summary>
    public float Angle => MathF.Atan2(Y, X);
}

/// <summary>Petites fonctions mathématiques utilisées partout.</summary>
public static class MathUtil
{
    public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    public static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

    /// <summary>Plus petite différence entre deux angles, entre -π et π (pour tourner du bon côté).</summary>
    public static float AngleDiff(float target, float current)
    {
        float d = target - current;
        return MathF.Atan2(MathF.Sin(d), MathF.Cos(d));
    }

    /// <summary>Fait tourner un angle vers une cible sans dépasser une vitesse maximale (radians par seconde).</summary>
    public static float TurnTowards(float current, float target, float maxStep)
        => current + Clamp(AngleDiff(target, current), -maxStep, maxStep);
}
