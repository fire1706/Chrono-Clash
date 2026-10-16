using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Dessins par défaut des chefs des ères 1 à 7 (le chef préhistorique est dans ArmyRenderer).
/// Ce sont des silhouettes simples en attendant les images du graphiste (art/chiefs/chief_<ère>.png).
/// Vue de dessus, l'avant vers +x, dans le repère déjà placé et tourné par l'appelant.
/// </summary>
public static class ChiefDrawings
{
    private static readonly Color Skin = new(0.79f, 0.55f, 0.38f);
    private static readonly Color Horse = new(0.45f, 0.31f, 0.2f);
    private static readonly Color Steel = new(0.66f, 0.68f, 0.72f);
    private static readonly Color Gold = new(0.95f, 0.79f, 0.3f);

    /// <summary>Couleur d'armure ou d'uniforme de chaque ère.</summary>
    private static readonly Color[] Main =
    {
        new(0.48f, 0.33f, 0.2f), new(0.72f, 0.2f, 0.16f), new(0.62f, 0.64f, 0.68f), new(0.45f, 0.25f, 0.45f),
        new(0.45f, 0.43f, 0.3f), new(0.36f, 0.42f, 0.26f), new(0.25f, 0.28f, 0.26f), new(0.82f, 0.9f, 1f),
    };

    public static void Draw(CanvasItem c, int era, Color team, float stride)
    {
        var main = Main[Math.Clamp(era, 0, 7)];
        switch (era)
        {
            case 2 or 3:
                // Chevalier / condottiere à cheval : le cheval dessous, le cavalier dessus.
                UnitDrawings.FillEllipse(c, new Vector2(2, 0), 17, 8, Horse);
                c.DrawCircle(new Vector2(17, 0), 5, Horse);                          // tête du cheval
                UnitDrawings.FillEllipse(c, new Vector2(0, 0), 10, 9, team);          // caparaçon du clan
                c.DrawCircle(Vector2.Zero, 7, main);
                c.DrawLine(new Vector2(2, 6), new Vector2(26, 6), new Color(0.4f, 0.28f, 0.15f), 2); // lance / épée
                c.DrawCircle(new Vector2(1, 0), 4.6f, Steel);                         // heaume
                break;
            case 5:
                // Général en jeep.
                c.DrawRect(new Rect2(-16, -11, 32, 22), new Color(0.32f, 0.38f, 0.24f));
                c.DrawRect(new Rect2(6, -10, 8, 20), new Color(0.24f, 0.29f, 0.18f)); // capot
                c.DrawRect(new Rect2(-14, -2, 20, 4), team);
                foreach (var w in new[] { new Vector2(10, -12), new Vector2(10, 12), new Vector2(-10, -12), new Vector2(-10, 12) }) c.DrawCircle(w, 3.2f, new Color(0.12f, 0.12f, 0.12f));
                c.DrawCircle(new Vector2(-2, 4), 4.5f, Skin);
                c.DrawCircle(new Vector2(-2, 4), 4.6f, main.Darkened(0.2f), false, 2);
                break;
            default:
                // Chef à pied : cape du clan, armure de l'ère, tête et coiffe.
                c.DrawCircle(new Vector2(stride * 5, -6), 3.2f, main.Darkened(0.4f));
                c.DrawCircle(new Vector2(-stride * 5, 6), 3.2f, main.Darkened(0.4f));
                UnitDrawings.FillEllipse(c, new Vector2(-4, 0), 10, 12, team);         // cape
                c.DrawCircle(Vector2.Zero, 9, main);
                c.DrawArc(Vector2.Zero, 7, -1.2f, 1.2f, 10, Gold, 2);                  // ceinturon doré
                c.DrawLine(new Vector2(4, 9), new Vector2(era >= 4 ? 18 : 16, 9), new Color(0.2f, 0.2f, 0.22f), 2.6f); // arme
                c.DrawCircle(new Vector2(1, 0), 5.6f, Skin);
                c.DrawArc(new Vector2(0.5f, 0), 5.2f, Mathf.Pi * 0.45f, Mathf.Pi * 1.55f, 10, era == 7 ? new Color(0.5f, 0.85f, 1f) : main.Darkened(0.3f), 3.2f); // casque
                if (era == 1) c.DrawLine(new Vector2(0, -7), new Vector2(0, 7), new Color(0.8f, 0.15f, 0.1f), 3); // crête du centurion
                if (era == 7) c.DrawArc(Vector2.Zero, 13, 0, Mathf.Tau, 24, new Color(0.5f, 0.85f, 1f, 0.5f), 2); // aura
                break;
        }
    }
}
