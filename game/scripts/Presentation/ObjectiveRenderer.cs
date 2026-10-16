using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Les objectifs de la carte vivante. Deux couches (deux nœuds) :
/// - au sol (<see cref="Overlay"/> = faux) : sanctuaires et portails, sous les troupes ;
/// - au-dessus (<see cref="Overlay"/> = vrai) : largage, boss et météores, au-dessus des troupes.
/// Images possibles pour le graphiste : art/objects/ (voir le guide du graphiste) — à brancher plus tard.
/// </summary>
public partial class ObjectiveRenderer : Node2D
{
    private static readonly Color[] ShrineColors = { new(0.36f, 0.82f, 1f), new(0.43f, 0.88f, 0.54f), new(1f, 0.48f, 0.35f) };
    private static readonly string[] ShrineIcons = { "≋", "+", "✦" };
    private static readonly Color Gold = new(0.95f, 0.79f, 0.3f);

    public bool Overlay { get; set; }

    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private float _time;

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

    private Rect2 View()
    {
        var size = GetViewportRect().Size / _camera.Zoom;
        return new Rect2(_camera.GetScreenCenterPosition() - size / 2 - new Vector2(200, 200), size + new Vector2(400, 400));
    }

    public override void _Draw()
    {
        var view = View();
        var world = _session.World;
        if (!Overlay)
        {
            foreach (var s in world.Shrines) if (view.HasPoint(s.Position.ToGodot())) DrawShrine(s);
            foreach (var p in world.Portals) if (view.HasPoint(p.Position.ToGodot())) DrawPortal(p);
            return;
        }
        if (world.Crate is { } crate && view.HasPoint(crate.Position.ToGodot())) DrawCrate(crate);
        if (world.Boss is { Dead: false } boss && view.HasPoint(boss.Position.ToGodot()) && _session.CanSee(boss.Position)) DrawBoss(boss);
        foreach (var m in world.Meteors) if (view.HasPoint(m.Position.ToGodot())) DrawMeteor(m);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void DrawShrine(Shrine s)
    {
        var c = s.Position.ToGodot();
        var col = ShrineColors[(int)s.Kind];
        DrawSetTransform(c + new Vector2(4, 6), 0, new Vector2(1, 0.68f));
        DrawCircle(Vector2.Zero, 44, new Color(0.04f, 0.06f, 0.02f, 0.25f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawCircle(c, 40, new Color(0.79f, 0.75f, 0.65f));
        DrawCircle(c, 30, new Color(0.87f, 0.83f, 0.74f));
        DrawArc(c, 40, 0, Mathf.Tau, 40, s.Owner != null ? CoreConvert.ColorOf(s.Owner.ColorRgb) : new Color(0.35f, 0.31f, 0.23f, 0.55f), s.Owner != null ? 5 : 2);
        if (s.Capturer != null && s.Progress > 0)
            DrawArc(c, 48, -Mathf.Pi / 2, -Mathf.Pi / 2 + s.Progress * Mathf.Tau, 40, CoreConvert.ColorOf(s.Capturer.ColorRgb), 6);
        DrawCircle(c, 18, new Color(col, 0.35f + Mathf.Sin(_time * 3) * 0.15f));
        DrawRect(new Rect2(c.X - 6, c.Y - 22, 12, 26), new Color(0.55f, 0.52f, 0.43f));   // stèle
        DrawRect(new Rect2(c.X - 4, c.Y - 20, 8, 8), col);
        DrawString(ThemeDB.FallbackFont, c + new Vector2(-20, 18), ShrineIcons[(int)s.Kind], HorizontalAlignment.Center, 40, 14, Colors.White);
    }

    private void DrawPortal(Portal p)
    {
        var c = p.Position.ToGodot();
        for (int i = 0; i < 3; i++)
        {
            // Trois anneaux en ellipse qui tournent en sens inverse.
            DrawSetTransform(c, p.Phase * (i % 2 == 1 ? 1 : -1), new Vector2(1, 0.64f));
            DrawArc(Vector2.Zero, 28 - i * 6, 0, Mathf.Tau, 32, i % 2 == 1 ? new Color(0.44f, 0.9f, 1f, 0.85f) : new Color(0.71f, 0.42f, 1f, 0.85f), 3);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        DrawCircle(c, 12, new Color(0.71f, 0.42f, 1f, 0.4f));
    }

    private void DrawCrate(Crate crate)
    {
        var c = crate.Position.ToGodot();
        float zoom = _camera.Zoom.X;
        if (!crate.Landed)
        {
            // Cercle d'annonce qui pulse, compte à rebours, puis la caisse qui tombe du ciel.
            float pulse = 1 + Mathf.Sin(_time * 6) * 0.08f;
            for (int i = 0; i < 24; i += 2)
                DrawArc(c, 46 * pulse, i * Mathf.Tau / 24, (i + 1) * Mathf.Tau / 24, 4, new Color(Gold, 0.9f), 3);
            DrawSetTransform(c + new Vector2(0, -60 / zoom), 0, new Vector2(1 / zoom, 1 / zoom));
            DrawString(ThemeDB.FallbackFont, new Vector2(-40, 0), $"{Mathf.CeilToInt(crate.TimeToLand)} s", HorizontalAlignment.Center, 80, 16, Gold);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            if (crate.TimeToLand < 2.5f)
            {
                float h = crate.TimeToLand / 2.5f * 420;
                var air = c + new Vector2(0, -h);
                DrawCircle(c, 18 * (1 - crate.TimeToLand / 2.5f), new Color(0, 0, 0, 0.3f));
                DrawArc(air + new Vector2(0, -26), 22, Mathf.Pi, Mathf.Tau, 16, new Color(0.95f, 0.95f, 0.9f), 10); // parachute
                DrawBox(air);
            }
            return;
        }
        DrawBox(c);
    }

    private void DrawBox(Vector2 c)
    {
        DrawRect(new Rect2(c - new Vector2(16, 16), new Vector2(32, 32)), new Color(0.55f, 0.37f, 0.2f));
        DrawRect(new Rect2(c - new Vector2(16, 3), new Vector2(32, 6)), Gold);
        DrawRect(new Rect2(c - new Vector2(3, 16), new Vector2(6, 32)), Gold);
        DrawRect(new Rect2(c - new Vector2(16, 16), new Vector2(32, 32)), new Color(0.3f, 0.2f, 0.1f), false, 2);
    }

    private void DrawBoss(Boss b)
    {
        var c = b.PreviousPosition.ToGodot().Lerp(b.Position.ToGodot(), _session.Alpha);
        var dark = new Color(0.45f, 0.1f, 0.1f);
        DrawSetTransform(c + new Vector2(6, 9), 0, new Vector2(1, 0.65f));
        DrawCircle(Vector2.Zero, 40, new Color(0, 0, 0, 0.3f));
        float stride = b.Velocity.Length > 5 ? Mathf.Sin(b.WalkCycle) : 0;
        switch (b.Form)
        {
            case 0: DrawSetTransform(c, b.Angle, new Vector2(2.6f, 2.6f)); UnitDrawings.Draw(this, TroopKind.Tank, 0, dark, b.WalkCycle, stride != 0); break;
            case 1:
                // Dragon : corps, ailes, cou et tête.
                DrawSetTransform(c, b.Angle, Vector2.One);
                float flap = Mathf.Sin(_time * 4) * 6;
                DrawColoredPolygon(new[] { new Vector2(-6, 0), new Vector2(-24, -48 - flap), new Vector2(10, -10) }, new Color(0.25f, 0.45f, 0.22f));
                DrawColoredPolygon(new[] { new Vector2(-6, 0), new Vector2(-24, 48 + flap), new Vector2(10, 10) }, new Color(0.25f, 0.45f, 0.22f));
                UnitDrawings.FillEllipse(this, Vector2.Zero, 30, 16, new Color(0.3f, 0.55f, 0.25f));
                DrawLine(new Vector2(-28, 0), new Vector2(-50, stride * 6), new Color(0.3f, 0.55f, 0.25f), 6);
                DrawCircle(new Vector2(36, 0), 11, new Color(0.35f, 0.6f, 0.28f));
                DrawCircle(new Vector2(42, -4), 2, new Color(1, 0.8f, 0.2f));
                DrawCircle(new Vector2(42, 4), 2, new Color(1, 0.8f, 0.2f));
                break;
            case 2: DrawSetTransform(c, b.Angle, new Vector2(2.4f, 2.4f)); UnitDrawings.Draw(this, TroopKind.Tank, 5, dark, b.WalkCycle, false); break;
            default: DrawSetTransform(c, b.Angle, new Vector2(2.6f, 2.6f)); UnitDrawings.Draw(this, TroopKind.Tank, 7, dark, b.WalkCycle, false); break;
        }
        DrawSetTransform(c, 0, Vector2.One);
        if (b.HitFlash > 0) DrawCircle(Vector2.Zero, 36, new Color(1, 1, 1, b.HitFlash / 0.12f * 0.5f));
        // Nom et barre de vie, à taille fixe à l'écran.
        float zoom = _camera.Zoom.X;
        DrawSetTransform(c + new Vector2(0, -48 - 16 / zoom), 0, new Vector2(1 / zoom, 1 / zoom));
        DrawRect(new Rect2(-60, -10, 120, 30), new Color(0.08f, 0.04f, 0.04f, 0.8f));
        DrawString(ThemeDB.FallbackFont, new Vector2(-60, 4), b.Name, HorizontalAlignment.Center, 120, 13, new Color(1, 0.55f, 0.45f));
        DrawRect(new Rect2(-56, 10, 112, 6), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(-56, 10, 112 * b.Hp / b.MaxHp, 6), new Color(0.85f, 0.25f, 0.2f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    private void DrawMeteor(Meteor m)
    {
        var c = m.Position.ToGodot();
        if (m.TimeToImpact > 1.5f) return;
        float q = 1 - m.TimeToImpact / 1.5f; // 0 → 1 jusqu'à l'impact
        DrawArc(c, 70, 0, Mathf.Tau, 32, new Color(1, 0.25f, 0.2f, 0.4f + q * 0.5f), 3);
        DrawCircle(c, 70 * q, new Color(1, 0.3f, 0.2f, 0.15f));
        var rock = c + new Vector2(-200, -500) * (1 - q);
        DrawLine(rock, rock + new Vector2(-60, -150), new Color(1, 0.6f, 0.2f, 0.5f), 6);
        DrawCircle(rock, 10, new Color(0.4f, 0.3f, 0.25f));
        DrawCircle(rock, 6, new Color(1, 0.6f, 0.25f));
    }
}
