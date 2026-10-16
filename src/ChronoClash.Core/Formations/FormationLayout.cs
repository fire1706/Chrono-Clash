using ChronoClash.Core.Commands;
using ChronoClash.Core.Data;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Formations;

/// <summary>
/// Transforme un dessin de formation et un nombre de troupes en places concrètes autour du chef.
/// Le dessin est un MODÈLE : s'il n'y a pas assez de places pour toutes les troupes, on l'agrandit
/// (en gardant ses proportions) jusqu'à ce que chacune ait la sienne.
/// Port fidèle de computeSlots du prototype HTML.
///
/// Coordonnées des places : X = distance vers l'avant du chef, Y = distance vers sa droite (pixels).
/// </summary>
public sealed class FormationLayout
{
    /// <summary>Distance minimale au chef (on ne colle pas les troupes contre lui).</summary>
    private const float MinChiefDistance = 24, MinChiefDistanceTank = 34;
    /// <summary>À chaque essai raté, on agrandit le dessin de 8 %.</summary>
    private const float GrowthStep = 1.08f;
    private const int MaxGrowthSteps = 70;

    /// <summary>Places par type de troupe (index = TroopKind).</summary>
    public List<Vec2>[] SlotsByKind { get; } = { new(), new(), new() };

    /// <summary>Agrandissement retenu (1 = taille du dessin).</summary>
    public float Scale { get; private set; } = 1;

    /// <summary>Arrondi « comme en JavaScript » (0,5 vers le haut), pour donner exactement les mêmes places que le prototype.</summary>
    private static int RoundHalfUp(float v) => (int)MathF.Floor(v + 0.5f);

    public static FormationLayout Compute(byte[] cells, int[] counts, GameData data)
    {
        var layout = new FormationLayout();
        // Combien de cases pour chaque type ? Un dessin vide utilise la formation « Ligne ».
        var area = new int[4];
        int total = 0;
        foreach (var v in cells) if (v != 0) { area[v]++; total++; }
        if (total == 0)
        {
            cells = FormationTemplate.Preset(0).Cells;
            foreach (var v in cells) if (v != 0) area[v]++;
        }

        // On cherche le plus petit agrandissement qui loge toutes les troupes.
        float scale = 1;
        var candidates = new List<Vec2>[3];
        for (int it = 0; it < MaxGrowthSteps; it++)
        {
            bool fits = true;
            for (int kind = 0; kind < 3; kind++)
            {
                if (counts[kind] == 0) continue;
                candidates[kind] = Candidates((TroopKind)kind, scale, cells, area, data);
                if (candidates[kind].Count < counts[kind]) { fits = false; break; }
            }
            if (fits) break;
            scale *= GrowthStep;
        }
        layout.Scale = scale;

        // On répartit les troupes régulièrement parmi les places disponibles (sans les entasser au même endroit).
        for (int kind = 0; kind < 3; kind++)
        {
            int n = counts[kind];
            if (n == 0) continue;
            var list = candidates[kind] ?? Candidates((TroopKind)kind, scale, cells, area, data);
            for (int i = 0; i < n; i++)
                layout.SlotsByKind[kind].Add(list.Count == 0 ? new Vec2(-40, 0) : list[Math.Min(list.Count - 1, (int)MathF.Floor((i + 0.5f) * list.Count / n))]);
        }
        return layout;
    }

    /// <summary>
    /// Toutes les places possibles pour un type de troupe : un quadrillage à l'espacement de ce type,
    /// filtré sur les cases peintes de sa couleur. Si le type n'a aucune case, il prend n'importe quelle case peinte.
    /// Les places sont listées de l'avant vers l'arrière, puis de gauche à droite.
    /// </summary>
    private static List<Vec2> Candidates(TroopKind kind, float scale, byte[] cells, int[] area, GameData data)
    {
        var result = new List<Vec2>();
        int code = (int)kind + 1;
        bool anyPainted = area[code] == 0;
        float spacing = data.Troop(kind).Spacing;
        float cellSize = FormationTemplate.CellWorldSize * scale;
        int steps = (int)MathF.Ceiling(Math.Max(FormationTemplate.Columns, FormationTemplate.Rows) * cellSize / 2 / spacing) + 1;
        float minDistance = kind == TroopKind.Tank ? MinChiefDistanceTank : MinChiefDistance;
        for (int fi = steps; fi >= -steps; fi--)
            for (int li = -steps; li <= steps; li++)
            {
                float f = fi * spacing, l = li * spacing;
                int row = RoundHalfUp(FormationTemplate.ChiefRow - f / cellSize);
                int col = RoundHalfUp(FormationTemplate.ChiefColumn + l / cellSize);
                if (row < 0 || col < 0 || row >= FormationTemplate.Rows || col >= FormationTemplate.Columns) continue;
                byte v = cells[row * FormationTemplate.Columns + col];
                if (anyPainted ? v == 0 : v != code) continue;
                if (MathF.Sqrt(f * f + l * l) < minDistance) continue;
                result.Add(new Vec2(f, l));
            }
        return result;
    }
}
