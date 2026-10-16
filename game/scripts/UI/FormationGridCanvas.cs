using ChronoClash.Core.Formations;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// La grille de dessin de l'éditeur de formation (17 × 13 cases). Le haut est l'avant.
/// Clic gauche maintenu = peindre avec le pinceau choisi · clic droit maintenu = effacer.
/// </summary>
public partial class FormationGridCanvas : Control
{
    public const float CellPixels = 28;

    /// <summary>Couleurs des cases : vide, guerriers, distance, tanks (mêmes couleurs que les bulles).</summary>
    public static readonly Color[] CellColors =
    {
        new(1, 1, 1, 0.06f), Color.FromHtml("#d9532f"), Color.FromHtml("#2a8fcc"), Color.FromHtml("#8a55d6"),
    };

    /// <summary>Les cases en cours d'édition (modifiées directement).</summary>
    public byte[] Cells = new byte[FormationTemplate.CellCount];
    /// <summary>Valeur peinte au clic gauche (1, 2, 3, ou 0 pour la gomme).</summary>
    public byte Brush = FormationTemplate.WarriorCell;

    /// <summary>Appelé pendant le dessin (pour rafraîchir l'aperçu).</summary>
    public event Action? Changed;
    /// <summary>Appelé quand on relâche la souris (pour envoyer le dessin à l'hôte et l'enregistrer).</summary>
    public event Action? EditFinished;

    private int _paintValue = -1;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(FormationTemplate.Columns * CellPixels, FormationTemplate.Rows * CellPixels);
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Draw()
    {
        var chiefCell = new Vector2(FormationTemplate.ChiefColumn, FormationTemplate.ChiefRow);
        for (int r = 0; r < FormationTemplate.Rows; r++)
            for (int c = 0; c < FormationTemplate.Columns; c++)
            {
                var rect = new Rect2(c * CellPixels + 1, r * CellPixels + 1, CellPixels - 2, CellPixels - 2);
                DrawRect(rect, CellColors[Cells[r * FormationTemplate.Columns + c]]);
            }
        // Le chef au centre (case non modifiable).
        var center = (chiefCell + new Vector2(0.5f, 0.5f)) * CellPixels;
        DrawCircle(center, CellPixels * 0.36f, Color.FromHtml("#f0b52a"));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-14, 5), "Chef", HorizontalAlignment.Center, 28, 10, new Color(0.15f, 0.1f, 0.05f));
        // Flèche « avant » au-dessus de la grille.
        float midX = Size.X / 2;
        DrawColoredPolygon(new[] { new Vector2(midX, -18), new Vector2(midX - 9, -6), new Vector2(midX + 9, -6) }, new Color(1, 1, 1, 0.8f));
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton button when button.ButtonIndex is MouseButton.Left or MouseButton.Right:
                if (button.Pressed)
                {
                    _paintValue = button.ButtonIndex == MouseButton.Left ? Brush : FormationTemplate.Empty;
                    PaintAt(button.Position);
                }
                else if (_paintValue >= 0)
                {
                    _paintValue = -1;
                    EditFinished?.Invoke();
                }
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _paintValue >= 0:
                PaintAt(motion.Position);
                AcceptEvent();
                break;
        }
    }

    private void PaintAt(Vector2 local)
    {
        int c = (int)(local.X / CellPixels), r = (int)(local.Y / CellPixels);
        if (c < 0 || r < 0 || c >= FormationTemplate.Columns || r >= FormationTemplate.Rows) return;
        if (c == FormationTemplate.ChiefColumn && r == FormationTemplate.ChiefRow) return;
        int i = r * FormationTemplate.Columns + c;
        if (Cells[i] == _paintValue) return;
        Cells[i] = (byte)_paintValue;
        QueueRedraw();
        Changed?.Invoke();
    }
}
