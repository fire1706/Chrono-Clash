using ChronoClash.Core.Data;
using ChronoClash.Core.Formations;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Aperçu de l'éditeur : montre où se placeraient réellement les troupes avec le dessin actuel,
/// pour le nombre de troupes de l'armée (au moins un petit échantillon si l'armée est encore vide).
/// </summary>
public partial class FormationPreviewCanvas : Control
{
    private byte[] _cells = new byte[FormationTemplate.CellCount];
    private int[] _counts = { 20, 10, 4 };
    private FormationLayout? _layout;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(240, 240);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Recalcule l'aperçu pour ce dessin et ces effectifs (guerriers, distance, tanks).</summary>
    public void Refresh(byte[] cells, int[] counts)
    {
        _cells = cells;
        _counts = counts[0] + counts[1] + counts[2] < 6 ? new[] { 20, 10, 4 } : counts;
        _layout = FormationLayout.Compute(_cells, _counts, GameData.Default);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.25f));
        if (_layout == null) return;
        // On cherche la place la plus éloignée pour que tout tienne dans le cadre.
        float extent = 40;
        foreach (var list in _layout.SlotsByKind) foreach (var s in list) extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(s.X), Mathf.Abs(s.Y)));
        float scale = (Size.X / 2 - 14) / extent;
        var center = Size / 2;
        for (int kind = 0; kind < 3; kind++)
        {
            float radius = kind == 2 ? 5 : 2.6f;
            foreach (var s in _layout.SlotsByKind[kind])
                // Repère de la formation : X = avant (vers le haut de l'aperçu), Y = droite.
                DrawCircle(center + new Vector2(s.Y, -s.X) * scale, radius, FormationGridCanvas.CellColors[kind + 1]);
        }
        DrawCircle(center, 5, Color.FromHtml("#f0b52a"));
        DrawString(ThemeDB.FallbackFont, new Vector2(6, Size.Y - 8), Localization.Loc.F("{0} troupes · ×{1:0.0}", _counts[0] + _counts[1] + _counts[2], _layout.Scale), HorizontalAlignment.Left, -1, 12, new Color(1, 1, 1, 0.7f));
    }
}
