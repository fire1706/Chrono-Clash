using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Map;

/// <summary>
/// Choisit les points de départ des clans : répartis en cercle autour du centre de la carte,
/// sur la terre ferme, sans eau autour, et sur une case de navigation libre.
/// </summary>
public static class SpawnPlanner
{
    public static void Plan(Terrain terrain, NavGrid nav, SimRandom random, int count)
    {
        const float W = Terrain.WorldSize;
        terrain.SpawnPoints.Clear();
        double baseAngle = random.NextDouble() * Math.PI * 2;
        for (int i = 0; i < count; i++)
        {
            Vec2? found = null;
            for (int k = 0; k < 400 && found == null; k++)
            {
                double a = baseAngle + i / (double)count * Math.PI * 2 + (random.NextDouble() - 0.5) * 0.4;
                double r = W * (0.28 + random.NextDouble() * 0.12);
                float x = MathUtil.Clamp((float)(W / 2 + Math.Cos(a) * r), 500, W - 500);
                float y = MathUtil.Clamp((float)(W / 2 + Math.Sin(a) * r), 500, W - 500);
                // Après 200 essais, on accepte un point même s'il y a de l'eau autour.
                if (terrain.IsWalkable(x, y, 40) && !nav.IsBlocked(NavGrid.CellOf(x, y)) && (k > 200 || terrain.IsOpenAround(x, y, 100)))
                    found = new Vec2(x, y);
            }
            // Dernier recours : n'importe où sur la terre ferme.
            for (int k = 0; k < 2000 && found == null; k++)
            {
                float x = 500 + (float)random.NextDouble() * (W - 1000), y = 500 + (float)random.NextDouble() * (W - 1000);
                if (terrain.IsWalkable(x, y, 40) && !nav.IsBlocked(NavGrid.CellOf(x, y))) found = new Vec2(x, y);
            }
            terrain.SpawnPoints.Add(found ?? new Vec2(W / 2, W / 2));
        }
    }
}
