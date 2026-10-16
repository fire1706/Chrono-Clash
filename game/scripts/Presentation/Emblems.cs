using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Les 8 emblèmes de clan, dessinés par le code (le graphiste pourra les remplacer par des images, voir son guide).
/// Ils apparaissent sur la bannière au-dessus du chef, dans le classement et dans le salon.
/// </summary>
public static class Emblems
{
    public static readonly string[] Names = { "Soleil", "Lune", "Étoile", "Couronne", "Épées", "Crâne", "Arbre", "Flamme" };

    /// <summary>Dessine l'emblème centré en <paramref name="c"/>, de rayon <paramref name="r"/>.</summary>
    public static void Draw(CanvasItem ci, int emblem, Vector2 c, float r, Color color, Color background)
    {
        switch (emblem % 8)
        {
            case 0: // Soleil : disque et rayons
                for (int i = 0; i < 8; i++) { var d = Vector2.FromAngle(i * Mathf.Tau / 8); ci.DrawLine(c + d * r * 0.6f, c + d * r, color, r * 0.18f); }
                ci.DrawCircle(c, r * 0.45f, color);
                break;
            case 1: // Lune : croissant (un disque recouvert par un disque de la couleur du fond)
                ci.DrawCircle(c, r * 0.85f, color);
                ci.DrawCircle(c + new Vector2(r * 0.35f, -r * 0.2f), r * 0.72f, background);
                break;
            case 2: // Étoile à 5 branches
                ci.DrawColoredPolygon(Star(c, r, r * 0.42f, 5), color);
                break;
            case 3: // Couronne
                ci.DrawColoredPolygon(new[] { c + new Vector2(-r * 0.85f, r * 0.55f), c + new Vector2(-r * 0.85f, -r * 0.45f), c + new Vector2(-r * 0.4f, 0),
                    c + new Vector2(0, -r * 0.75f), c + new Vector2(r * 0.4f, 0), c + new Vector2(r * 0.85f, -r * 0.45f), c + new Vector2(r * 0.85f, r * 0.55f) }, color);
                break;
            case 4: // Épées croisées
                ci.DrawLine(c + new Vector2(-r * 0.8f, r * 0.8f), c + new Vector2(r * 0.8f, -r * 0.8f), color, r * 0.22f);
                ci.DrawLine(c + new Vector2(r * 0.8f, r * 0.8f), c + new Vector2(-r * 0.8f, -r * 0.8f), color, r * 0.22f);
                ci.DrawLine(c + new Vector2(-r * 0.75f, r * 0.35f), c + new Vector2(-r * 0.35f, r * 0.75f), color, r * 0.2f);
                ci.DrawLine(c + new Vector2(r * 0.75f, r * 0.35f), c + new Vector2(r * 0.35f, r * 0.75f), color, r * 0.2f);
                break;
            case 5: // Crâne
                ci.DrawCircle(c + new Vector2(0, -r * 0.12f), r * 0.68f, color);
                ci.DrawRect(new Rect2(c + new Vector2(-r * 0.35f, r * 0.3f), new Vector2(r * 0.7f, r * 0.5f)), color);
                ci.DrawCircle(c + new Vector2(-r * 0.27f, -r * 0.12f), r * 0.18f, background);
                ci.DrawCircle(c + new Vector2(r * 0.27f, -r * 0.12f), r * 0.18f, background);
                break;
            case 6: // Arbre
                ci.DrawRect(new Rect2(c + new Vector2(-r * 0.12f, r * 0.2f), new Vector2(r * 0.24f, r * 0.7f)), color);
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r * 0.95f), c + new Vector2(r * 0.75f, r * 0.35f), c + new Vector2(-r * 0.75f, r * 0.35f) }, color);
                break;
            default: // Flamme
                ci.DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(r * 0.55f, -r * 0.1f), c + new Vector2(r * 0.6f, r * 0.45f),
                    c + new Vector2(0, r * 0.9f), c + new Vector2(-r * 0.6f, r * 0.45f), c + new Vector2(-r * 0.35f, -r * 0.2f), c + new Vector2(-r * 0.1f, r * 0.05f) }, color);
                break;
        }
    }

    private static Vector2[] Star(Vector2 c, float outer, float inner, int points)
    {
        var pts = new Vector2[points * 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = c + Vector2.FromAngle(-Mathf.Pi / 2 + i * Mathf.Pi / points) * (i % 2 == 0 ? outer : inner);
        return pts;
    }
}

/// <summary>Petit carré d'interface qui affiche un emblème (choix de l'emblème dans le menu).</summary>
public partial class EmblemPreview : Control
{
    public int Emblem { get; set; }
    public Color Tint { get; set; } = new(0.94f, 0.71f, 0.16f);

    public override void _Ready() => CustomMinimumSize = new Vector2(36, 36);

    public override void _Draw()
    {
        var bg = new Color(0.12f, 0.1f, 0.08f);
        DrawRect(new Rect2(Vector2.Zero, Size), bg);
        Emblems.Draw(this, Emblem, Size / 2, Mathf.Min(Size.X, Size.Y) * 0.38f, Tint, bg);
    }
}
