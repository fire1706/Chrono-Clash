using ChronoClash.Core.Mathematics;
using ChronoClash.Game.Net;
using ChronoClash.Game.Presentation;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Flèches au bord de l'écran vers les objectifs hors de vue : largage (jaune), boss (rouge),
/// portail le plus proche (violet), avec leur distance.
/// </summary>
public partial class ObjectiveIndicators : Control
{
    private GameSession _session = null!;
    private Camera2D _camera = null!;

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var world = _session.World;
        var g = _session.LocalArmy.General;
        if (g.Dead) return;
        var targets = new List<(Vec2 Position, Color Color, string Icon)>();
        if (world.Crate is { } crate) targets.Add((crate.Position, new Color(0.95f, 0.79f, 0.3f), "▣"));
        if (world.Boss is { Dead: false } boss) targets.Add((boss.Position, new Color(1f, 0.35f, 0.27f), "☠"));
        var nearest = world.Portals.OrderBy(p => Vec2.Distance(p.Position, g.Position)).FirstOrDefault();
        if (nearest != null && Vec2.Distance(nearest.Position, g.Position) < 3200) targets.Add((nearest.Position, new Color(0.79f, 0.61f, 1f), "◎"));

        var size = GetViewportRect().Size;
        var center = size / 2;
        var cam = _camera.GetScreenCenterPosition();
        float zoom = _camera.Zoom.X;
        var font = ThemeDB.FallbackFont;
        foreach (var (pos, color, icon) in targets)
        {
            var screen = (pos.ToGodot() - cam) * zoom + center;
            if (screen.X > 30 && screen.X < size.X - 30 && screen.Y > 30 && screen.Y < size.Y - 30) continue; // déjà visible
            var d = screen - center;
            float k = Mathf.Min((size.X / 2 - 50) / Mathf.Max(1e-3f, Mathf.Abs(d.X)), (size.Y / 2 - 70) / Mathf.Max(1e-3f, Mathf.Abs(d.Y)));
            var edge = center + d * k;
            float angle = d.Angle();
            DrawCircle(edge, 15, new Color(0.08f, 0.06f, 0.04f, 0.82f));
            DrawArc(edge, 15, 0, Mathf.Tau, 24, color, 2);
            DrawString(font, edge + new Vector2(-15, 5), icon, HorizontalAlignment.Center, 30, 14, color);
            var tip = edge + Vector2.FromAngle(angle) * 25;
            DrawColoredPolygon(new[] { tip, edge + Vector2.FromAngle(angle + 0.45f) * 17, edge + Vector2.FromAngle(angle - 0.45f) * 17 }, color);
            DrawString(font, edge + new Vector2(-30, 32), $"{Mathf.RoundToInt(Vec2.Distance(pos, g.Position) / 10)} m", HorizontalAlignment.Center, 60, 11, Colors.White);
        }
    }
}
