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

    /// <summary>Couleur de chaque type de troupe, lue une fois (lire « #d9532f » à chaque image coûte cher).</summary>
    private Color[] KindColors = Array.Empty<Color>();

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
        KindColors = session.World.Data.Troops.OrderBy(t => (int)t.Kind).Select(t => Color.FromHtml(t.Color)).ToArray();
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        float zoom = _camera.Zoom.X;
        var view = VisibleWorldRect(80);
        float alpha = _session.Alpha;
        bool simple = zoom < SimpleDrawZoom || Settings.GameSettings.Current.LowQuality;
        foreach (var army in _session.World.Armies)
        {
            if (!army.Alive) continue;
            var team = CoreConvert.ColorOf(army.ColorRgb);
            bool mine = army == _session.LocalArmy;
            foreach (var u in army.Units)
            {
                if (u.Dead) continue;
                if (!mine && !_session.CanSee(u.Position)) continue; // caché par le brouillard de guerre
                var pos = u.PreviousPosition.ToGodot().Lerp(u.Position.ToGodot(), alpha);
                if (!view.HasPoint(pos)) continue;
                if (simple) { DrawSimple(u, pos, team); continue; }
                DrawDetailed(u, pos, team);
                DrawOverlays(u, pos, zoom);
            }
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>
    /// Par-dessus la troupe (sans rotation) : lueur du cri de guerre, éclair blanc quand elle est touchée,
    /// lueur d'évolution, barre de vie si elle est blessée, chevrons de vétéran.
    /// </summary>
    private void DrawOverlays(Unit u, Vector2 pos, float zoom)
    {
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        float r = u.Radius;
        if (u.WarCryTime > 0) DrawArc(pos, r + 4, 0, Mathf.Tau, 16, new Color(1, 0.55f, 0.15f, 0.7f), 2);
        if (u.HitFlash > 0) DrawCircle(pos, r + 1, new Color(1, 1, 1, u.HitFlash / 0.12f * 0.6f));
        if (u.EvolveFlash > 0) DrawArc(pos, r + 10 * (1 - u.EvolveFlash / 0.8f) + 3, 0, Mathf.Tau, 20, new Color(1, 0.95f, 0.69f, u.EvolveFlash / 0.8f), 3);
        if (u.Hp < u.MaxHp)
        {
            float w = r * 2.4f, y = pos.Y - r - 7;
            DrawRect(new Rect2(pos.X - w / 2, y, w, 3), new Color(0, 0, 0, 0.55f));
            float q = u.Hp / u.MaxHp;
            DrawRect(new Rect2(pos.X - w / 2, y, w * q, 3), q > 0.5f ? new Color(0.38f, 0.76f, 0.48f) : q > 0.25f ? new Color(0.91f, 0.73f, 0.23f) : new Color(0.85f, 0.3f, 0.2f));
        }
        if (u.Veteran > 0)
        {
            // Chevrons dorés au-dessus de la troupe (un par niveau).
            float y = pos.Y - r - (u.Hp < u.MaxHp ? 12 : 8);
            for (int i = 0; i < u.Veteran; i++)
            {
                float cy = y - i * 3;
                DrawPolyline(new[] { new Vector2(pos.X - 4, cy), new Vector2(pos.X, cy - 2.5f), new Vector2(pos.X + 4, cy) }, new Color(0.95f, 0.79f, 0.3f), 1.6f);
            }
        }
    }

    private void DrawSimple(Unit u, Vector2 pos, Color team)
    {
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        float r = u.Kind == Core.Commands.TroopKind.Tank ? 11 : 5;
        DrawCircle(pos, r, u.HitFlash > 0 ? Colors.White : team);
        DrawCircle(pos, r * 0.45f, KindColors[(int)u.Kind]);
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
