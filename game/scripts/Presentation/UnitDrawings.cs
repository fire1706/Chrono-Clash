using ChronoClash.Core.Commands;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Dessins par défaut des troupes, faits par le code (style du prototype HTML), vus de dessus avec l'avant vers +x.
/// Ère 0 (Préhistoire) : guerrier à massue, frondeur, mammouth, comme dans le prototype.
/// Autres ères : silhouettes provisoires aux couleurs de l'ère ; les vrais dessins arrivent avec l'étape M3
/// (évolution des ères) ou directement sous forme d'images du graphiste.
/// Toutes les méthodes dessinent dans le repère courant : l'appelant a déjà placé et tourné la troupe.
/// </summary>
public static class UnitDrawings
{
    private static readonly Color Skin = new(0.79f, 0.55f, 0.38f);
    private static readonly Color Hair = new(0.24f, 0.15f, 0.09f);
    private static readonly Color Fur = new(0.48f, 0.33f, 0.2f);
    private static readonly Color FurLight = new(0.6f, 0.45f, 0.28f);
    private static readonly Color Leather = new(0.29f, 0.19f, 0.13f);
    private static readonly Color Wood = new(0.42f, 0.27f, 0.14f);
    private static readonly Color WoodDark = new(0.33f, 0.21f, 0.1f);
    private static readonly Color Bone = new(0.94f, 0.9f, 0.8f);
    private static readonly Color Stone = new(0.55f, 0.53f, 0.5f);
    private static readonly Color MammothFur = new(0.43f, 0.29f, 0.18f);
    private static readonly Color MammothDark = new(0.33f, 0.22f, 0.13f);
    public static readonly Color Shadow = new(0.04f, 0.06f, 0.02f, 0.28f);

    /// <summary>Couleur principale provisoire de chaque ère (armures, uniformes…).</summary>
    private static readonly Color[] EraColors =
    {
        new(0.48f, 0.33f, 0.2f), new(0.72f, 0.2f, 0.16f), new(0.55f, 0.57f, 0.6f), new(0.45f, 0.25f, 0.45f),
        new(0.42f, 0.42f, 0.3f), new(0.36f, 0.4f, 0.26f), new(0.38f, 0.4f, 0.36f), new(0.75f, 0.82f, 0.9f),
    };

    private static readonly Vector2[] UnitCircle = MakeCircle(18);
    private static Vector2[] MakeCircle(int n)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++) pts[i] = Vector2.FromAngle(i * Mathf.Tau / n);
        return pts;
    }

    /// <summary>Ellipse pleine (Godot n'a qu'un cercle : on dessine un polygone).</summary>
    public static void FillEllipse(CanvasItem c, Vector2 center, float rx, float ry, Color color)
    {
        var pts = new Vector2[UnitCircle.Length];
        for (int i = 0; i < pts.Length; i++) pts[i] = center + new Vector2(UnitCircle[i].X * rx, UnitCircle[i].Y * ry);
        c.DrawColoredPolygon(pts, color);
    }

    /// <summary>Ombre portée au sol, sous la troupe.</summary>
    public static void DrawShadow(CanvasItem c, TroopKind kind)
    {
        if (kind == TroopKind.Tank) FillEllipse(c, new Vector2(3, 4), 19, 13, Shadow);
        else FillEllipse(c, new Vector2(2, 3), 7.5f, 6, Shadow);
    }

    /// <summary>Dessine une troupe. <paramref name="walk"/> = phase de marche, <paramref name="moving"/> = en mouvement.</summary>
    public static void Draw(CanvasItem c, TroopKind kind, int era, Color team, float walk, bool moving)
    {
        float stride = moving ? Mathf.Sin(walk) : 0;
        if (era == 0)
        {
            switch (kind)
            {
                case TroopKind.Warrior: ClubWarrior(c, team, stride); return;
                case TroopKind.Ranged: Slinger(c, team, stride, walk); return;
                case TroopKind.Tank: Mammoth(c, team, stride); return;
            }
        }
        switch (kind)
        {
            case TroopKind.Tank: GenericVehicle(c, team, era); break;
            default: GenericSoldier(c, kind, team, era, stride); break;
        }
    }

    // ================= Ère préhistorique =================

    private static void ClubWarrior(CanvasItem c, Color team, float stride)
    {
        c.DrawCircle(new Vector2(stride * 4, -3.5f), 2.4f, Leather);       // jambes
        c.DrawCircle(new Vector2(-stride * 4, 3.5f), 2.4f, Leather);
        c.DrawCircle(Vector2.Zero, 6.2f, Fur);                              // corps en fourrure
        c.DrawArc(Vector2.Zero, 4.6f, -1.2f, 1.2f, 8, team, 2f);            // ceinture à la couleur du clan
        c.DrawCircle(new Vector2(3, -5), 1.8f, Skin);                       // main gauche
        var hand = new Vector2(3, 5.5f);
        var clubEnd = hand + new Vector2(9, 1).Rotated(0.3f + stride * 0.2f);
        c.DrawCircle(hand, 1.9f, Skin);
        c.DrawLine(hand, clubEnd, Wood, 2.2f);                              // massue
        c.DrawCircle(clubEnd, 2.6f, WoodDark);
        c.DrawCircle(new Vector2(1, 0), 3.8f, Skin);                        // tête
        c.DrawArc(new Vector2(0.4f, 0), 3f, Mathf.Pi * 0.6f, Mathf.Pi * 1.4f, 8, Hair, 2.2f);
    }

    private static void Slinger(CanvasItem c, Color team, float stride, float walk)
    {
        c.DrawCircle(new Vector2(stride * 4, -3.2f), 2.2f, Leather);
        c.DrawCircle(new Vector2(-stride * 4, 3.2f), 2.2f, Leather);
        c.DrawCircle(Vector2.Zero, 5.6f, FurLight);
        c.DrawArc(Vector2.Zero, 4.2f, -1.3f, 1.3f, 8, team, 2f);
        var hand = new Vector2(2.5f, -5.5f);
        c.DrawCircle(hand, 1.8f, Skin);
        // Fronde qui tourne au-dessus de la main.
        float a = walk * 1.6f;
        var stone = hand + new Vector2(5, 0).Rotated(a);
        c.DrawLine(hand, stone, new Color(0.7f, 0.6f, 0.45f), 1f);
        c.DrawCircle(stone, 1.6f, Stone);
        c.DrawCircle(new Vector2(3, 4.5f), 1.7f, Skin);
        c.DrawCircle(new Vector2(1, 0), 3.5f, Skin);
        c.DrawArc(new Vector2(0.3f, 0), 2.8f, Mathf.Pi * 0.6f, Mathf.Pi * 1.4f, 8, Hair, 2f);
    }

    private static void Mammoth(CanvasItem c, Color team, float stride)
    {
        // Pattes
        c.DrawCircle(new Vector2(7 + stride * 3, -8), 3.6f, MammothDark);
        c.DrawCircle(new Vector2(7 - stride * 3, 8), 3.6f, MammothDark);
        c.DrawCircle(new Vector2(-7 - stride * 3, -8), 3.6f, MammothDark);
        c.DrawCircle(new Vector2(-7 + stride * 3, 8), 3.6f, MammothDark);
        FillEllipse(c, Vector2.Zero, 16, 11, MammothFur);                   // corps
        FillEllipse(c, new Vector2(-2, 0), 8, 7, team);                     // couverture à la couleur du clan
        FillEllipse(c, new Vector2(-2, 0), 5, 4.5f, team.Darkened(0.25f));
        FillEllipse(c, new Vector2(11, -6.5f), 4, 5, MammothDark);          // oreilles
        FillEllipse(c, new Vector2(11, 6.5f), 4, 5, MammothDark);
        c.DrawCircle(new Vector2(14, 0), 6.5f, MammothFur);                 // tête
        foreach (int side in new[] { -1, 1 })                               // défenses en ivoire
            c.DrawPolyline(new[] { new Vector2(17, side * 3.5f), new Vector2(22, side * 6.5f), new Vector2(26, side * 5.5f), new Vector2(27.5f, side * 2.5f) }, Bone, 2.4f);
        c.DrawLine(new Vector2(19, 0), new Vector2(26 + stride, stride * 2), MammothDark, 3.2f); // trompe
    }

    // ================= Silhouettes provisoires (ères 1 à 7) =================

    private static void GenericSoldier(CanvasItem c, TroopKind kind, Color team, int era, float stride)
    {
        var main = EraColors[Math.Clamp(era, 0, 7)];
        c.DrawCircle(new Vector2(stride * 4, -3.5f), 2.3f, main.Darkened(0.4f));
        c.DrawCircle(new Vector2(-stride * 4, 3.5f), 2.3f, main.Darkened(0.4f));
        c.DrawCircle(Vector2.Zero, 6f, main);
        c.DrawArc(Vector2.Zero, 4.4f, -1.2f, 1.2f, 8, team, 2f);
        // Arme : courte au corps à corps, longue pour les tireurs.
        float length = kind == TroopKind.Ranged ? 13 : 9;
        c.DrawLine(new Vector2(2, 4.5f), new Vector2(2 + length, 4.5f), new Color(0.2f, 0.2f, 0.22f), 2f);
        c.DrawCircle(new Vector2(1, 0), 3.6f, Skin);
        c.DrawArc(new Vector2(0.6f, 0), 3.3f, Mathf.Pi * 0.5f, Mathf.Pi * 1.5f, 8, main.Darkened(0.3f), 2.4f); // casque
    }

    private static void GenericVehicle(CanvasItem c, Color team, int era)
    {
        var main = EraColors[Math.Clamp(era, 0, 7)];
        c.DrawRect(new Rect2(-15, -11, 30, 22), main.Darkened(0.35f));     // chenilles / châssis
        c.DrawRect(new Rect2(-13, -8, 26, 16), main);
        c.DrawRect(new Rect2(-13, -2, 26, 4), team);                        // bande à la couleur du clan
        c.DrawCircle(new Vector2(1, 0), 6.5f, main.Lightened(0.15f));       // tourelle
        c.DrawLine(new Vector2(4, 0), new Vector2(22, 0), main.Darkened(0.5f), 3f); // canon
    }
}
