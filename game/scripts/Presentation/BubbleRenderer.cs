using ChronoClash.Core.Entities;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Dessine les bulles à ramasser : halo lumineux, bulle à la couleur du type de troupe, petit symbole,
/// léger flottement. Les étoiles dorées sont dessinées en forme d'étoile.
/// </summary>
public partial class BubbleRenderer : Node2D
{
    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private float _time;
    private static readonly Color StarColor = Color.FromHtml("#f2c94c");

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = GetViewportRect().Size / _camera.Zoom;
        var center = _camera.GetScreenCenterPosition();
        var view = new Rect2(center - size / 2 - new Vector2(40, 40), size + new Vector2(80, 80));
        var data = _session.World.Data;
        foreach (var b in _session.World.Bubbles)
        {
            var p = b.Position.ToGodot();
            if (!view.HasPoint(p)) continue;
            float pop = Mathf.Min(1, b.Age * 3);             // apparition progressive
            float bob = Mathf.Sin(_time * 2.2f + b.Phase) * 2.5f; // flottement
            float r = b.Radius * pop;
            var color = b.Kind == BubbleKind.Star ? StarColor : Color.FromHtml(data.Troop((Core.Commands.TroopKind)(int)b.Kind).Color);

            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            UnitDrawings.FillEllipse(this, p + new Vector2(2, r * 0.9f), r * 0.85f, r * 0.35f, UnitDrawings.Shadow); // ombre
            var c = p + new Vector2(0, bob - 3);
            DrawCircle(c, r * 1.9f, new Color(color, 0.10f));  // halo
            DrawCircle(c, r * 1.4f, new Color(color, 0.18f));
            if (b.Kind == BubbleKind.Star) { DrawStar(c, r * 1.1f, color); continue; }
            DrawCircle(c, r, color);
            DrawCircle(c, r * 0.72f, color.Lightened(0.25f));
            DrawCircle(c + new Vector2(-r * 0.35f, -r * 0.38f), r * 0.25f, new Color(1, 1, 1, 0.55f)); // reflet
            DrawSymbol(b.Kind, c, r);
        }
    }

    /// <summary>Symbole du type : épée (guerrier), flèche (distance), bouclier carré (tank).</summary>
    private void DrawSymbol(BubbleKind kind, Vector2 c, float r)
    {
        var ink = new Color(0.12f, 0.08f, 0.05f, 0.85f);
        float s = r * 0.45f;
        switch (kind)
        {
            case BubbleKind.Warrior:
                DrawLine(c + new Vector2(-s, s), c + new Vector2(s, -s), ink, 2);
                DrawLine(c + new Vector2(-s * 0.6f, -s * 0.1f), c + new Vector2(s * 0.1f, s * 0.6f), ink, 2);
                break;
            case BubbleKind.Ranged:
                DrawLine(c + new Vector2(-s, s), c + new Vector2(s, -s), ink, 1.8f);
                DrawColoredPolygon(new[] { c + new Vector2(s, -s), c + new Vector2(s * 0.1f, -s * 0.8f), c + new Vector2(s * 0.8f, -s * 0.1f) }, ink);
                break;
            case BubbleKind.Tank:
                DrawRect(new Rect2(c - new Vector2(s, s), new Vector2(s * 2, s * 2)), ink, false, 2);
                break;
        }
    }

    private void DrawStar(Vector2 c, float r, Color color)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5 + _time * 0.6f;
            pts[i] = c + Vector2.FromAngle(a) * (i % 2 == 0 ? r : r * 0.45f);
        }
        DrawColoredPolygon(pts, color);
        DrawCircle(c, r * 0.25f, new Color(1, 1, 1, 0.7f));
    }
}
