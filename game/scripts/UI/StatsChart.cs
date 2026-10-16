using ChronoClash.Game.Localization;
using ChronoClash.Game.Stats;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// La courbe de fin de partie : troupes, ennemis abattus, ressources et points au fil du temps, chaque courbe
/// avec sa couleur et son style de trait (lisible même en distinguant mal les couleurs). Le fond est découpé
/// en bandes : une bande par ère traversée.
/// </summary>
public partial class StatsChart : Control
{
    private static readonly (string Label, Color Color, float Width, float Dash)[] Series =
    {
        ("Troupes", Color.FromHtml("#2a8fcc"), 3.5f, 0), ("Ennemis abattus", Color.FromHtml("#d9432f"), 2.5f, 0),
        ("Ressources ◆", Color.FromHtml("#d99a1a"), 2.5f, 8), ("Points ★", Color.FromHtml("#8a55d6"), 2.5f, 3),
    };
    private static readonly Color[] EraBands =
    {
        new(0.48f, 0.33f, 0.2f, 0.18f), new(0.72f, 0.2f, 0.16f, 0.16f), new(0.55f, 0.57f, 0.6f, 0.16f), new(0.45f, 0.25f, 0.45f, 0.16f),
        new(0.45f, 0.43f, 0.3f, 0.16f), new(0.36f, 0.42f, 0.26f, 0.16f), new(0.38f, 0.4f, 0.36f, 0.16f), new(0.75f, 0.82f, 0.9f, 0.16f),
    };

    public List<StatSample> Samples { get; set; } = new();

    public override void _Ready() => CustomMinimumSize = new Vector2(700, 240);

    private static int Value(StatSample s, int series) => series switch { 0 => s.Troops, 1 => s.Kills, 2 => s.Resources, _ => s.Points };

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var area = new Rect2(40, 10, Size.X - 50, Size.Y - 60);
        DrawRect(area, new Color(0, 0, 0, 0.25f));
        if (Samples.Count < 2)
        {
            DrawString(font, area.Position + new Vector2(10, 30), Loc.T("Partie trop courte pour une courbe."), HorizontalAlignment.Left, -1, 14);
            return;
        }
        double t0 = Samples[0].Time, t1 = Math.Max(t0 + 1, Samples[^1].Time);
        float X(double t) => area.Position.X + (float)((t - t0) / (t1 - t0)) * area.Size.X;

        // Bandes des ères.
        for (int i = 0; i < Samples.Count - 1; i++)
            DrawRect(new Rect2(X(Samples[i].Time), area.Position.Y, X(Samples[i + 1].Time) - X(Samples[i].Time), area.Size.Y), EraBands[Math.Clamp(Samples[i].Era, 0, 7)]);

        // Courbes : chacune à sa propre échelle (sa valeur maximale atteint le haut du cadre).
        for (int k = 0; k < Series.Length; k++)
        {
            var (_, color, width, dash) = Series[k];
            float max = Math.Max(1, Samples.Max(s => Value(s, k)));
            var points = Samples.Select(s => new Vector2(X(s.Time), area.End.Y - Value(s, k) / max * area.Size.Y)).ToArray();
            for (int i = 0; i < points.Length - 1; i++)
            {
                if (dash > 0) DrawDashedLine(points[i], points[i + 1], color, width, dash);
                else DrawLine(points[i], points[i + 1], color, width, true);
            }
            DrawString(font, points[^1] + new Vector2(-40, -6), $"{Value(Samples.MaxBy(s => Value(s, k)), k)}", HorizontalAlignment.Left, -1, 11, color);
        }

        // Axe du temps (minutes) et légende.
        int minutes = (int)((t1 - t0) / 60);
        for (int m = 0; m <= minutes; m++)
            DrawString(font, new Vector2(X(t0 + m * 60) - 6, area.End.Y + 14), $"{m}", HorizontalAlignment.Left, -1, 11, new Color(1, 1, 1, 0.6f));
        float lx = area.Position.X;
        foreach (var (label, color, _, _) in Series)
        {
            DrawRect(new Rect2(lx, Size.Y - 18, 14, 8), color);
            string text = Loc.T(label);
            DrawString(font, new Vector2(lx + 18, Size.Y - 10), text, HorizontalAlignment.Left, -1, 12);
            lx += 34 + font.GetStringSize(text, HorizontalAlignment.Left, -1, 12).X;
        }
        DrawString(font, new Vector2(Size.X - 120, Size.Y - 10), Loc.T("Bandes : ères"), HorizontalAlignment.Left, -1, 12, new Color(1, 1, 1, 0.6f));
    }
}
