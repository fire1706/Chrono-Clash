using ChronoClash.Core.Mathematics;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>Conversions entre les types du cœur (Vec2, couleurs RVB) et ceux de Godot.</summary>
public static class CoreConvert
{
    public static Vector2 ToGodot(this Vec2 v) => new(v.X, v.Y);
    public static Vec2 ToCore(this Vector2 v) => new(v.X, v.Y);

    /// <summary>Couleur d'un clan (0xRRGGBB) vers une couleur Godot.</summary>
    public static Color ColorOf(uint rgb, float alpha = 1f)
        => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
}
