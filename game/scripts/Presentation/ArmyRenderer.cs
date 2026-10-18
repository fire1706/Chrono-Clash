using ChronoClash.Core.Entities;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Dessine les chefs (et plus tard les troupes), le chemin du joueur et les repères de clic.
/// Les positions sont interpolées entre deux ticks de simulation pour un mouvement fluide.
///
/// Le dessin du chef reprend le « chef de clan » préhistorique du prototype. Le graphiste pourra le remplacer
/// par une image (art/chiefs/chief_0.png, voir docs/ART_GUIDE.md) : ce sera branché à l'étape M2.
/// </summary>
public partial class ArmyRenderer : Node2D
{
    private static readonly Color Skin = new(0.79f, 0.55f, 0.38f);
    private static readonly Color Fur = new(0.48f, 0.33f, 0.2f);
    private static readonly Color DarkLeather = new(0.29f, 0.19f, 0.13f);
    private static readonly Color Bone = new(0.94f, 0.9f, 0.8f);
    private static readonly Color Shadow = new(0.04f, 0.06f, 0.02f, 0.3f);

    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private readonly UnitVisualRegistry _visuals = new();
    /// <summary>Repères de clic droit : position et âge en secondes.</summary>
    private readonly List<(Vector2 Position, float Age)> _markers = new();
    private float _time;

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
    }

    /// <summary>Affiche un cercle qui s'agrandit là où le joueur a cliqué.</summary>
    public void AddMarker(Vector2 worldPosition) => _markers.Add((worldPosition, 0));

    /// <summary>Position affichée d'un chef (glissée entre le tick précédent et le tick courant).</summary>
    public Vector2 DisplayPosition(General g) => g.PreviousPosition.ToGodot().Lerp(g.Position.ToGodot(), _session.Alpha);

    public override void _Process(double delta)
    {
        _time += (float)delta;
        for (int i = _markers.Count - 1; i >= 0; i--)
        {
            var m = _markers[i];
            m.Age += (float)delta;
            if (m.Age > 1.2f) _markers.RemoveAt(i); else _markers[i] = m;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float zoom = _camera.Zoom.X;
        var local = _session.LocalArmy;
        DrawPath(local, zoom);
        foreach (var (pos, age) in _markers)
        {
            float q = age / 1.2f, r = 10 + q * 22;
            DrawSetTransform(pos, 0, new Vector2(1, 0.6f));
            DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 32, new Color(1, 1, 1, 1 - q), 2 / zoom);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        foreach (var army in _session.World.Armies)
        {
            if (!army.Alive || army.General.Dead) continue;
            if (army != local && (!army.ChiefVisible || !_session.CanSee(army.General.Position))) continue; // brouillard de guerre
            DrawChief(army, DisplayPosition(army.General), army == local);
        }
        foreach (var army in _session.World.Armies)
        {
            if (!army.Alive || army.General.Dead) continue;
            if (army != local && (!army.ChiefVisible || !_session.CanSee(army.General.Position))) continue;
            DrawNameTag(army, DisplayPosition(army.General), army == local, zoom);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Chemin en pointillés jusqu'à la destination, avec un petit drapeau au bout.</summary>
    private void DrawPath(Army army, float zoom)
    {
        var g = army.General;
        if (g.Path.Count == 0) return;
        var color = new Color(1, 1, 1, 0.85f);
        var from = DisplayPosition(g);
        foreach (var step in g.Path)
        {
            var to = step.ToGodot();
            DrawDashedLine(from, to, color, 2.2f / zoom, 10 / zoom);
            from = to;
        }
        var end = g.Path[^1].ToGodot();
        var team = CoreConvert.ColorOf(army.ColorRgb);
        DrawLine(end, end + new Vector2(0, -26), team, 2.5f / zoom);
        DrawColoredPolygon(new[] { end + new Vector2(0, -26), end + new Vector2(15, -21), end + new Vector2(0, -16) }, team);
    }

    /// <summary>Le chef de clan : ombre, anneau, cône de direction, puis le personnage vu de dessus.</summary>
    private void DrawChief(Army army, Vector2 pos, bool isLocal)
    {
        var g = army.General;
        var team = CoreConvert.ColorOf(army.ColorRgb);

        // Ombre au sol (ellipse = cercle écrasé).
        DrawSetTransform(pos + new Vector2(3, 5), 0, new Vector2(1, 0.65f));
        DrawCircle(Vector2.Zero, 17, Shadow);

        // Anneau en pointillés qui tourne, et cône qui montre l'avant (futur avant de la formation).
        DrawSetTransform(pos, 0, Vector2.One);
        if (isLocal)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.Tau / 12 + _time * 1.2f;
                DrawArc(Vector2.Zero, 24, a, a + 0.32f, 5, new Color(team, 0.75f), 2);
            }
            var wedge = new List<Vector2>();
            for (int i = 0; i <= 8; i++) wedge.Add(Vector2.FromAngle(g.Angle - 0.42f + 0.84f * i / 8) * 38);
            for (int i = 8; i >= 0; i--) wedge.Add(Vector2.FromAngle(g.Angle - 0.42f + 0.84f * i / 8) * 28);
            DrawColoredPolygon(wedge.ToArray(), new Color(team, 0.28f));
        }

        // Le personnage, tourné dans sa direction : l'avant est vers +x.
        DrawSetTransform(pos, g.Angle, Vector2.One);
        bool moving = g.Velocity.Length > 15;
        float stride = moving ? Mathf.Sin(g.WalkCycle) : 0;
        var (image, teamMask) = g.Promoted ? _visuals.Get(g.HeirKind, g.Era) : _visuals.GetChief(g.Era);
        if (image != null)
        {
            // Image du graphiste (art/chiefs/chief_<ère>.png, ou celle du soldat pour un chef promu).
            var size = image.GetSize();
            float target = g.Promoted ? UnitVisualRegistry.WorldSizeOf(g.HeirKind) * 1.25f : UnitVisualRegistry.ChiefWorldSize(g.Era);
            float scale = target / Mathf.Max(size.X, size.Y);
            var rect = new Rect2(-size * scale / 2, size * scale);
            DrawTextureRect(image, rect, false);
            if (teamMask != null) DrawTextureRect(teamMask, rect, false, team);
        }
        else if (g.Promoted)
        {
            // Un soldat promu garde son apparence, un peu plus grand.
            DrawSetTransform(pos, g.Angle, new Vector2(1.25f, 1.25f));
            UnitDrawings.Draw(this, g.HeirKind, g.Era, team, g.WalkCycle, moving);
        }
        else if (g.Era == 0) DrawClanChief(team, stride);
        else ChiefDrawings.Draw(this, g.Era, team, stride);

        // Effets par-dessus (sans rotation) : éclair quand il est touché, lueur d'évolution, étoile de grade.
        DrawSetTransform(pos, 0, Vector2.One);
        if (g.HitFlash > 0) DrawCircle(Vector2.Zero, 15, new Color(1, 1, 1, g.HitFlash / 0.12f * 0.55f));
        if (g.EvolveFlash > 0) DrawArc(Vector2.Zero, 18 + 16 * (1 - g.EvolveFlash / 0.8f), 0, Mathf.Tau, 28, new Color(1, 0.95f, 0.69f, g.EvolveFlash / 0.8f), 3);
        if (g.Promoted) DrawPromotionStar(new Vector2(0, -22));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Le chef de clan préhistorique (dessin du prototype), dans le repère déjà tourné.</summary>
    private void DrawClanChief(Color team, float stride)
    {
        // Jambes
        DrawCircle(new Vector2(stride * 6, -5.5f), 3.4f, DarkLeather);
        DrawCircle(new Vector2(-stride * 6, 5.5f), 3.4f, DarkLeather);
        // Plumes derrière la tête
        DrawCircle(new Vector2(-12, -3), 2.8f, new Color(0.75f, 0.22f, 0.17f));
        DrawCircle(new Vector2(-13, 0), 3f, team);
        DrawCircle(new Vector2(-12, 3), 2.8f, new Color(0.18f, 0.53f, 0.67f));
        // Cape en fourrure (bord dentelé)
        var cloak = new Vector2[14];
        for (int i = 0; i < 14; i++)
        {
            float a = i / 14f * Mathf.Tau, r = i % 2 == 1 ? 10.5f : 12.5f;
            cloak[i] = new Vector2(Mathf.Cos(a) * r - 1, Mathf.Sin(a) * r);
        }
        DrawColoredPolygon(cloak, Fur);
        DrawArc(new Vector2(-1, 0), 8, -1.1f, 1.1f, 10, team, 2.4f); // écharpe à la couleur du clan
        // Mains et massue (la massue se balance en marchant)
        DrawCircle(new Vector2(5, -10), 3, Skin);
        float swing = 0.35f + stride * 0.18f;
        var hand = new Vector2(4, 9.5f);
        var clubEnd = hand + new Vector2(15, 2).Rotated(swing);
        DrawCircle(hand, 3, Skin);
        DrawLine(hand, clubEnd, new Color(0.42f, 0.27f, 0.14f), 3.2f);
        DrawCircle(clubEnd + new Vector2(2, 0.5f).Rotated(swing), 4.2f, new Color(0.35f, 0.22f, 0.11f));
        // Tête, cheveux, bandeau et cornes en os
        DrawCircle(new Vector2(1, 0), 6.6f, Skin);
        DrawArc(new Vector2(-0.5f, 0), 4.6f, Mathf.Pi * 0.55f, Mathf.Pi * 1.45f, 10, new Color(0.24f, 0.15f, 0.09f), 4);
        DrawArc(new Vector2(1, 0), 6.4f, -1.9f, 1.9f, 12, team, 2.2f);
        foreach (int side in new[] { -1, 1 })
            DrawPolyline(new[] { new Vector2(1, side * 5.5f), new Vector2(3.5f, side * 10), new Vector2(7, side * 12), new Vector2(10, side * 12.5f) }, Bone, 2.6f);
        DrawCircle(new Vector2(7.4f, 0), 1.6f, Bone);

    }

    /// <summary>Petite étoile dorée : ce chef est un soldat qui a repris le commandement.</summary>
    private void DrawPromotionStar(Vector2 c)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++) pts[i] = c + Vector2.FromAngle(-Mathf.Pi / 2 + i * Mathf.Pi / 5) * (i % 2 == 0 ? 6 : 2.6f);
        DrawColoredPolygon(pts, new Color(0.95f, 0.79f, 0.3f));
    }

    /// <summary>Étiquette au-dessus du chef, toujours à la même taille à l'écran quel que soit le zoom.</summary>
    private void DrawNameTag(Army army, Vector2 pos, bool isLocal, float zoom)
    {
        var font = ThemeDB.FallbackFont;
        string label = (isLocal ? Localization.Loc.T("Toi") : Localization.Loc.T(army.Name)) + (army.Evolution != null ? " · " + Localization.Loc.T("évolution") : "");
        const int fontSize = 13;
        float width = font.GetStringSize(label, HorizontalAlignment.Left, -1, fontSize).X + 14;
        // On se place au-dessus du chef puis on annule le zoom pour dessiner « en pixels d'écran ».
        DrawSetTransform(pos + new Vector2(0, -30 - 14 / zoom), 0, new Vector2(1 / zoom, 1 / zoom));
        DrawRect(new Rect2(-width / 2, -9, width, 18), new Color(0.08f, 0.06f, 0.04f, 0.78f));
        // Bannière avec l'emblème du clan, à gauche de l'étiquette.
        var banner = new Color(0.08f, 0.06f, 0.04f, 0.9f);
        DrawRect(new Rect2(-width / 2 - 22, -11, 20, 22), banner);
        Emblems.Draw(this, army.Emblem, new Vector2(-width / 2 - 12, 0), 8, CoreConvert.ColorOf(army.ColorRgb), banner);
        DrawString(font, new Vector2(-width / 2, 5), label, HorizontalAlignment.Center, width, fontSize, CoreConvert.ColorOf(army.ColorRgb));
        // Barres : vie (vert) et endurance (jaune).
        var g = army.General;
        DrawRect(new Rect2(-width / 2, 11, width, 4), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(-width / 2, 11, width * g.Hp / g.MaxHp, 4), new Color(0.38f, 0.76f, 0.48f));
        if (isLocal)
        {
            DrawRect(new Rect2(-width / 2, 16, width, 3), new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(-width / 2, 16, width * g.Stamina / 100f, 3), g.Exhausted ? new Color(0.75f, 0.31f, 0.23f) : new Color(0.91f, 0.73f, 0.23f));
        }
    }
}
