namespace ChronoClash.Core.Formations;

/// <summary>
/// Un dessin de formation : une grille de 17 × 13 cases autour du chef (case centrale),
/// où chaque case vaut 0 (vide), 1 (guerriers), 2 (troupes à distance) ou 3 (tanks).
/// Le haut de la grille est l'AVANT : la direction où marche le chef.
/// </summary>
public sealed class FormationTemplate
{
    public const int Columns = 17, Rows = 13;
    /// <summary>Case du chef (au centre de la grille).</summary>
    public const int ChiefColumn = 8, ChiefRow = 6;
    public const int CellCount = Columns * Rows;
    /// <summary>Taille d'une case dans le monde quand la formation n'est pas agrandie (pixels).</summary>
    public const float CellWorldSize = 24;

    /// <summary>Valeurs possibles d'une case.</summary>
    public const byte Empty = 0, WarriorCell = 1, RangedCell = 2, TankCell = 3;

    public FormationTemplate(string name, byte[] cells)
    {
        if (cells.Length != CellCount) throw new ArgumentException($"Une formation a {CellCount} cases");
        Name = name;
        Cells = cells;
    }

    public string Name { get; set; }
    public byte[] Cells { get; }

    public byte this[int column, int row]
    {
        get => Cells[row * Columns + column];
        set { if (!(column == ChiefColumn && row == ChiefRow)) Cells[row * Columns + column] = value; }
    }

    public FormationTemplate Clone() => new(Name, (byte[])Cells.Clone());

    /// <summary>Vrai si le tableau est une formation valide (bonne taille, valeurs 0 à 3). Sert à vérifier les ordres reçus.</summary>
    public static bool IsValid(byte[] cells)
    {
        if (cells.Length != CellCount) return false;
        foreach (var c in cells) if (c > TankCell) return false;
        return true;
    }

    // ================= Les 4 formations de départ =================

    public static readonly string[] PresetNames = { "Ligne", "Carré", "Coin", "Colonne" };

    /// <summary>
    /// Les dessins de départ, décrits par une règle : pour chaque case (dc = écart de colonne au chef,
    /// dr = écart de ligne, négatif = devant), quel type de troupe y mettre.
    /// </summary>
    private static readonly Func<int, int, byte>[] PresetRules =
    {
        // Ligne : tanks devant, guerriers au milieu, troupes à distance derrière.
        (dc, dr) => dr == -3 && Math.Abs(dc) <= 6 ? TankCell
                  : (dr == -2 || dr == -1) && Math.Abs(dc) <= 7 ? WarriorCell
                  : dr == 0 && Math.Abs(dc) >= 2 && Math.Abs(dc) <= 7 ? WarriorCell
                  : (dr == 2 || dr == 3) && Math.Abs(dc) <= 6 ? RangedCell : Empty,
        // Carré : tanks tout autour, guerriers à l'intérieur, tireurs protégés au centre.
        (dc, dr) =>
        {
            int m = Math.Max(Math.Abs(dc), Math.Abs(dr));
            return m == 5 ? TankCell : (m == 4 || m == 3) ? WarriorCell : m <= 2 && m > 0 ? RangedCell : Empty;
        },
        // Coin : pointe de tanks pour percer, guerriers en éventail, tireurs à l'arrière.
        (dc, dr) =>
        {
            int a = Math.Abs(dc);
            if (dr <= -4 && dr >= -5 && a <= dr + 6) return TankCell;
            if (dr >= -3 && dr <= 1 && a <= dr + 6 && !(a <= 1 && dr >= -1)) return WarriorCell;
            if (dr >= 3 && dr <= 4 && a <= 5) return RangedCell;
            return Empty;
        },
        // Colonne : étroite, pour voyager et passer en forêt.
        (dc, dr) =>
        {
            int a = Math.Abs(dc);
            if (dr <= -5 && a <= 1) return TankCell;
            if (dr >= -4 && dr <= 3 && a <= 2 && !(a == 0 && dr == 0)) return WarriorCell;
            if (dr >= 4 && a <= 2) return RangedCell;
            return Empty;
        },
    };

    /// <summary>Une nouvelle copie de la formation de départ numéro <paramref name="index"/> (0 à 3).</summary>
    public static FormationTemplate Preset(int index)
    {
        var rule = PresetRules[index % PresetRules.Length];
        var cells = new byte[CellCount];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                if (r == ChiefRow && c == ChiefColumn) continue;
                cells[r * Columns + c] = rule(c - ChiefColumn, r - ChiefRow);
            }
        return new FormationTemplate(PresetNames[index % PresetNames.Length], cells);
    }
}
