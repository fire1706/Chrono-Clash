using ChronoClash.Core.Data;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// Brouillard de guerre d'un clan : une grille (une case tous les 80 px) où chaque case est « vue » si elle est
/// proche du chef (560 px) ou d'une troupe (230 px, tanks 280 px). Recalculée 4 fois par seconde.
/// Sert à l'affichage du joueur ET aux bots, qui ne voient donc que ce que verrait un humain.
/// </summary>
public sealed class VisionMap
{
    public VisionMap(int cell)
    {
        Cell = cell;
        Size = (Terrain.WorldSize + cell - 1) / cell;
        Visible = new bool[Size * Size];
    }

    public int Cell { get; }
    /// <summary>Nombre de cases par côté.</summary>
    public int Size { get; }
    public bool[] Visible { get; }

    public bool IsVisible(float x, float y)
    {
        int i = (int)(x / Cell), j = (int)(y / Cell);
        if (i < 0 || j < 0 || i >= Size || j >= Size) return false;
        return Visible[j * Size + i];
    }

    public bool IsVisible(Vec2 p) => IsVisible(p.X, p.Y);

    /// <summary>Recalcule la vision d'un clan à partir de son chef et de ses troupes.</summary>
    public void Compute(Army army, VisionTuning tuning)
    {
        Array.Clear(Visible);
        if (!army.Alive) return;
        if (!army.General.Dead) Stamp(army.General.Position, tuning.Chief);
        foreach (var u in army.Units)
            if (!u.Dead) Stamp(u.Position, u.Kind == Commands.TroopKind.Tank ? tuning.Tank : tuning.Troop);
    }

    private void Stamp(Vec2 p, float radius)
    {
        int i0 = Math.Max(0, (int)((p.X - radius) / Cell)), i1 = Math.Min(Size - 1, (int)((p.X + radius) / Cell));
        int j0 = Math.Max(0, (int)((p.Y - radius) / Cell)), j1 = Math.Min(Size - 1, (int)((p.Y + radius) / Cell));
        float r2 = radius * radius;
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float cx = (i + 0.5f) * Cell - p.X, cy = (j + 0.5f) * Cell - p.Y;
                if (cx * cx + cy * cy <= r2) Visible[j * Size + i] = true;
            }
    }
}
