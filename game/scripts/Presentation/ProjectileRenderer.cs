using ChronoClash.Core.Entities;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Dessine les projectiles en vol. Pierres, flèches et rochers suivent une trajectoire en cloche
/// (ils montent puis retombent) ; balles et obus laissent une traînée lumineuse ; le plasma brille.
/// Le graphiste pourra fournir des images dans art/projectiles/ (voir le guide du graphiste).
/// </summary>
public partial class ProjectileRenderer : Node2D
{
    private GameSession _session = null!;
    private Camera2D _camera = null!;

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var size = GetViewportRect().Size / _camera.Zoom;
        var view = new Rect2(_camera.GetScreenCenterPosition() - size / 2 - new Vector2(100, 100), size + new Vector2(200, 200));
        foreach (var p in _session.World.Projectiles)
        {
            float t = Mathf.Clamp(p.Elapsed / p.FlightTime, 0, 1);
            var from = p.From.ToGodot();
            var to = p.To.ToGodot();
            var ground = from.Lerp(to, t);
            if (!view.HasPoint(ground) || !_session.CanSee(p.To)) continue;
            float height = p.ArcHeight * 4 * t * (1 - t); // parabole : 0 au départ et à l'arrivée
            var pos = ground + new Vector2(0, -height);
            float angle = (to - from).Angle();
            if (p.ArcHeight > 0)
            {
                // Petite ombre au sol, et orientation qui suit la courbe (on regarde où il sera un instant plus tard).
                DrawCircle(ground, 2, new Color(0, 0, 0, 0.25f));
                float t2 = Mathf.Min(1, t + 0.03f);
                var next = from.Lerp(to, t2) + new Vector2(0, -p.ArcHeight * 4 * t2 * (1 - t2));
                if (next.DistanceSquaredTo(pos) > 0.0001f) angle = (next - pos).Angle();
            }
            var dir = Vector2.FromAngle(angle);
            switch (p.Kind)
            {
                case "stone": DrawCircle(pos, 2.4f, new Color(0.55f, 0.53f, 0.5f)); break;
                case "rock": DrawCircle(pos, 5 + height / 40, new Color(0.47f, 0.44f, 0.4f)); DrawCircle(pos - new Vector2(1.5f, 1.5f), 2, new Color(0.62f, 0.6f, 0.56f)); break;
                case "arrow":
                    DrawLine(pos - dir * 7, pos + dir * 5, new Color(0.45f, 0.3f, 0.15f), 1.6f);
                    DrawColoredPolygon(new[] { pos + dir * 7, pos + dir * 3 + dir.Orthogonal() * 2, pos + dir * 3 - dir.Orthogonal() * 2 }, new Color(0.3f, 0.3f, 0.32f));
                    break;
                case "ball": DrawCircle(pos, 2.6f, new Color(0.18f, 0.18f, 0.2f)); break;
                case "bullet": DrawLine(pos - dir * 10, pos, new Color(1, 0.9f, 0.5f, 0.85f), 1.6f); break;
                case "shell": DrawLine(pos - dir * 14, pos, new Color(1, 0.75f, 0.4f, 0.8f), 2.6f); DrawCircle(pos, 2.4f, new Color(0.35f, 0.33f, 0.28f)); break;
                case "rocket":
                    DrawLine(pos - dir * 18, pos - dir * 4, new Color(0.85f, 0.85f, 0.85f, 0.45f), 4); // fumée
                    DrawLine(pos - dir * 4, pos + dir * 5, new Color(0.3f, 0.35f, 0.25f), 3);
                    DrawCircle(pos - dir * 5, 2.4f, new Color(1, 0.6f, 0.2f));
                    break;
                case "plasma":
                    DrawCircle(pos, 9, new Color(0.4f, 0.8f, 1, 0.25f));
                    DrawCircle(pos, 4.5f, new Color(0.55f, 0.9f, 1));
                    DrawCircle(pos, 2, Colors.White);
                    break;
                default: DrawCircle(pos, 2.5f, Colors.White); break;
            }
        }
    }
}
