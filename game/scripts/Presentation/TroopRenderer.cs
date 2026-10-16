using ChronoClash.Core.Entities;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Dessine toutes les troupes visibles à l'écran. Pour chaque troupe : une image du graphiste si elle existe
/// (voir <see cref="UnitVisualRegistry"/>), sinon le dessin par défaut (<see cref="UnitDrawings"/>).
/// Quand on dézoome beaucoup, les troupes deviennent de simples pastilles (beaucoup plus rapide à dessiner).
/// </summary>
public partial class TroopRenderer : Node2D
{
    /// <summary>En dessous de ce zoom, les troupes sont dessinées en pastilles.</summary>
    private const float SimpleDrawZoom = 0.5f;

    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private readonly UnitVisualRegistry _visuals = new();

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        float zoom = _camera.Zoom.X;
        var view = VisibleWorldRect(80);
        float alpha = _session.Alpha;
        bool simple = zoom < SimpleDrawZoom;
        foreach (var army in _session.World.Armies)
        {
            if (!army.Alive) continue;
            var team = CoreConvert.ColorOf(army.ColorRgb);
            foreach (var u in army.Units)
            {
                if (u.Dead) continue;
                var pos = u.PreviousPosition.ToGodot().Lerp(u.Position.ToGodot(), alpha);
                if (!view.HasPoint(pos)) continue;
                if (simple) { DrawSimple(u, pos, team); continue; }
                DrawDetailed(u, pos, team);
            }
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void DrawSimple(Unit u, Vector2 pos, Color team)
    {
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        float r = u.Kind == Core.Commands.TroopKind.Tank ? 11 : 5;
        DrawCircle(pos, r, team);
        DrawCircle(pos, r * 0.45f, Color.FromHtml(_session.World.Data.Troop(u.Kind).Color));
    }

    private void DrawDetailed(Unit u, Vector2 pos, Color team)
    {
        // Petite animation d'apparition : la troupe « gonfle » pendant 0,3 s.
        float pop = Mathf.Min(1, 0.4f + u.Age * 2f);
        DrawSetTransform(pos, u.Angle, new Vector2(pop, pop));
        UnitDrawings.DrawShadow(this, u.Kind);
        var (body, teamMask) = _visuals.Get(u.Kind, u.Era);
        if (body != null)
        {
            // Image du graphiste, centrée et mise à la bonne taille.
            var size = body.GetSize();
            float scale = UnitVisualRegistry.WorldSizeOf(u.Kind) / Mathf.Max(size.X, size.Y);
            var rect = new Rect2(-size * scale / 2, size * scale);
            DrawTextureRect(body, rect, false);
            if (teamMask != null) DrawTextureRect(teamMask, rect, false, team);
        }
        else UnitDrawings.Draw(this, u.Kind, u.Era, team, u.WalkCycle, u.Velocity.Length > 12);
    }

    /// <summary>Zone du monde affichée, agrandie d'une marge (pour ne pas voir les troupes apparaître au bord).</summary>
    private Rect2 VisibleWorldRect(float margin)
    {
        var size = GetViewportRect().Size / _camera.Zoom;
        var center = _camera.GetScreenCenterPosition();
        return new Rect2(center - size / 2 - new Vector2(margin, margin), size + new Vector2(margin * 2, margin * 2));
    }
}
