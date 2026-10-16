using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;

namespace ChronoClash.Core.Map;

/// <summary>
/// Génère le terrain à partir de la graine et du thème (port fidèle du prototype HTML).
/// Étapes : relief et végétation → rivières → gués (archipel) → niveau des collines → rochers → ruines.
/// Pour modifier l'aspect de la carte, les réglages sont dans <see cref="ThemeDef"/> et dans les constantes ci-dessous.
/// </summary>
public static class MapGenerator
{
    // ---- Échelles du bruit (plus grand = formes plus larges) ----
    private const double ReliefScale = 1500, ForestScale = 700, DirtScale = 380;
    /// <summary>Distance au bord sur laquelle la terre plonge dans la mer.</summary>
    private const float CoastWidth = 600;

    public static Terrain Generate(MatchSettings settings)
    {
        var theme = ThemeDef.Get(settings.Theme);
        var terrain = new Terrain(theme, settings.Seed);
        var random = new SimRandom(settings.Seed);
        var noise = new ValueNoise(random);

        FillGrids(terrain, noise);
        CarveRivers(terrain, noise, random);
        if (theme.Fords) CarveFords(terrain, random);
        ComputeHillLevel(terrain, random);
        var center = FindCentralOpenSpot(terrain);
        PlaceRocks(terrain, random, center);
        PlaceRuins(terrain, random);
        return terrain;
    }

    /// <summary>Altitude, forêts et terre pour chaque point de la grille.</summary>
    private static void FillGrids(Terrain t, ValueNoise noise)
    {
        const int W = Terrain.WorldSize;
        for (int j = 0; j <= Terrain.GridCount; j++)
            for (int i = 0; i <= Terrain.GridCount; i++)
            {
                double x = i * Terrain.GridCell, y = j * Terrain.GridCell;
                int k = j * Terrain.GridPoints + i;
                double h = noise.Fbm(x / ReliefScale, y / ReliefScale, 5);
                // e vaut 0 au bord de la carte et 1 à l'intérieur : la terre descend vers la mer au bord.
                double e = MathUtil.Clamp(Math.Min(Math.Min(x, y), Math.Min(W - x, W - y)) / CoastWidth, 0, 1);
                h -= (1 - e) * (1 - e) * 0.45;
                t.Height[k] = (float)(h + t.Theme.HeightOffset * e);
                t.ForestDensity[k] = (float)noise.Fbm(x / ForestScale + 100, y / ForestScale + 50, 3);
                t.DirtDensity[k] = (float)noise.Fbm(x / DirtScale + 200, y / DirtScale - 70, 2);
            }
    }

    /// <summary>Abaisse le terrain le long d'un cercle pour y mettre de l'eau peu profonde (ou le remonter pour un gué).</summary>
    private static void Stamp(Terrain t, double x, double y, double width, Func<double, float, float> apply)
    {
        int i0 = Math.Max(0, (int)Math.Floor((x - width) / Terrain.GridCell)), i1 = Math.Min(Terrain.GridCount, (int)Math.Ceiling((x + width) / Terrain.GridCell));
        int j0 = Math.Max(0, (int)Math.Floor((y - width) / Terrain.GridCell)), j1 = Math.Min(Terrain.GridCount, (int)Math.Ceiling((y + width) / Terrain.GridCell));
        for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                double d = Math.Sqrt(Math.Pow(i * Terrain.GridCell - x, 2) + Math.Pow(j * Terrain.GridCell - y, 2));
                if (d > width) continue;
                int k = j * Terrain.GridPoints + i;
                t.Height[k] = apply(d / width, t.Height[k]);
            }
    }

    /// <summary>Rivières sinueuses qui traversent la carte d'un bord à l'autre. Toujours franchissables à gué.</summary>
    private static void CarveRivers(Terrain t, ValueNoise noise, SimRandom r)
    {
        const double W = Terrain.WorldSize;
        for (int rv = 0; rv < t.Theme.Rivers; rv++)
        {
            // Point de départ sur un bord choisi au hasard, direction vers l'intérieur.
            double x, y, a;
            int side = (int)(r.NextDouble() * 4);
            if (side == 0) { x = r.NextDouble() * W; y = 0; a = Math.PI / 2; }
            else if (side == 1) { x = W; y = r.NextDouble() * W; a = Math.PI; }
            else if (side == 2) { x = r.NextDouble() * W; y = W; a = -Math.PI / 2; }
            else { x = 0; y = r.NextDouble() * W; a = 0; }
            double a0 = a, wave = r.NextDouble() * 100;
            for (int step = 0; step < 1600; step++)
            {
                // La direction ondule doucement mais garde son cap général.
                a += (noise.Sample(step * 0.05 + wave, rv * 9.1) - 0.5) * 0.32 - (a - a0) * 0.015;
                x += Math.Cos(a) * 16; y += Math.Sin(a) * 16;
                if (x < -60 || y < -60 || x > W + 60 || y > W + 60) break;
                double width = 34 + noise.Sample(step * 0.03, wave) * 30;
                // Plus profond au milieu de la rivière, mais jamais en eau profonde.
                Stamp(t, x, y, width, (q, h) => { float target = Terrain.WaterLevel - 0.006f - 0.02f * (float)(1 - q); return h > target ? target : h; });
            }
        }
    }

    /// <summary>Archipel : gués peu profonds en étoile et en anneau qui relient toutes les îles.</summary>
    private static void CarveFords(Terrain t, SimRandom r)
    {
        const double W = Terrain.WorldSize;
        float fordLevel = Terrain.WaterLevel - t.Theme.DeepOffset + 0.012f;
        void Ford(double x0, double y0, double x1, double y1)
        {
            int n = (int)Math.Ceiling(Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) / 16);
            for (int s = 0; s <= n; s++)
                Stamp(t, x0 + (x1 - x0) * s / n, y0 + (y1 - y0) * s / n, 46, (_, h) => h < fordLevel ? fordLevel : h);
        }
        double cx = W / 2, cy = W / 2, ring = W * 0.34, start = r.NextDouble() * 6.28;
        for (int k = 0; k < 12; k++) { double a = start + k / 12.0 * 6.283; Ford(cx, cy, cx + Math.Cos(a) * W * 0.45, cy + Math.Sin(a) * W * 0.45); }
        for (int k = 0; k < 48; k++) { double a = k / 48.0 * 6.283, b = (k + 1) / 48.0 * 6.283; Ford(cx + Math.Cos(a) * ring, cy + Math.Sin(a) * ring, cx + Math.Cos(b) * ring, cy + Math.Sin(b) * ring); }
    }

    /// <summary>Les collines sont les 14 % de terres les plus hautes.</summary>
    private static void ComputeHillLevel(Terrain t, SimRandom r)
    {
        var heights = new List<float>();
        for (int i = 0; i < 6000; i++)
        {
            float h = t.HeightAt((float)(r.NextDouble() * Terrain.WorldSize), (float)(r.NextDouble() * Terrain.WorldSize));
            if (h > Terrain.WaterLevel + 0.03f) heights.Add(h);
        }
        heights.Sort();
        t.HillLevel = heights.Count > 0 ? heights[(int)(heights.Count * 0.86)] : 1f;
    }

    /// <summary>Un endroit dégagé près du centre (on n'y met pas de rochers).</summary>
    private static Vec2 FindCentralOpenSpot(Terrain t)
    {
        float c = Terrain.WorldSize / 2f;
        for (int radius = 0; radius < 2500; radius += 40)
            for (float a = 0; a < 6.28f; a += 0.3f)
            {
                float x = c + MathF.Cos(a) * radius, y = c + MathF.Sin(a) * radius;
                if (t.HeightAt(x, y) > Terrain.WaterLevel + 0.06f && t.ForestAt(x, y) < 0.55f && t.IsOpenAround(x, y, 160)) return new Vec2(x, y);
            }
        return new Vec2(c, c);
    }

    /// <summary>Rochers aux contours irréguliers, sur la terre ferme, hors des forêts.</summary>
    private static void PlaceRocks(Terrain t, SimRandom r, Vec2 keepClear)
    {
        const float W = Terrain.WorldSize;
        for (int k = 0; k < 7000 && t.Rocks.Count < t.Theme.MaxRocks; k++)
        {
            float x = 300 + (float)r.NextDouble() * (W - 600), y = 300 + (float)r.NextDouble() * (W - 600);
            if (t.HeightAt(x, y) < Terrain.WaterLevel + 0.05f || t.ForestAt(x, y) > 0.56f || Vec2.Distance(new Vec2(x, y), keepClear) < 380) continue;
            float radius = 14 + (float)(r.NextDouble() * r.NextDouble()) * 44; // surtout des petits rochers, quelques gros
            if (t.Rocks.Any(o => Vec2.Distance(new Vec2(o.X, o.Y), new Vec2(x, y)) < o.Radius + radius + 30)) continue;
            int n = 8 + (int)(r.NextDouble() * 4);
            var outline = new float[n * 2];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * MathF.PI * 2, m = 0.78f + (float)r.NextDouble() * 0.32f;
                outline[i * 2] = MathF.Cos(a) * radius * m;
                outline[i * 2 + 1] = MathF.Sin(a) * radius * m;
            }
            t.AddRock(new Rock { X = x, Y = y, Radius = radius, Outline = outline });
        }
    }

    /// <summary>Ruines : cercles de piliers en pierre, bien espacés les uns des autres.</summary>
    private static void PlaceRuins(Terrain t, SimRandom r)
    {
        const float W = Terrain.WorldSize;
        for (int k = 0; k < 4000 && t.Ruins.Count < 22; k++)
        {
            float x = 500 + (float)r.NextDouble() * (W - 1000), y = 500 + (float)r.NextDouble() * (W - 1000);
            var p = new Vec2(x, y);
            if (t.HeightAt(x, y) < Terrain.WaterLevel + 0.06f || t.ForestAt(x, y) > 0.56f
                || t.Ruins.Any(o => Vec2.Distance(new Vec2(o.X, o.Y), p) < 1200)
                || t.Rocks.Any(o => Vec2.Distance(new Vec2(o.X, o.Y), p) < o.Radius + 110)) continue;
            int n = 6 + (int)(r.NextDouble() * 4);
            float radius = 58 + (float)r.NextDouble() * 30;
            t.Ruins.Add(new Ruin(x, y, radius));
            for (int i = 0; i < n; i++)
            {
                if (r.NextDouble() < 0.18) continue; // quelques piliers manquants
                float a = i / (float)n * MathF.PI * 2, h = 7 + (float)r.NextDouble() * 3;
                t.AddRock(new Rock
                {
                    X = x + MathF.Cos(a) * radius, Y = y + MathF.Sin(a) * radius, Radius = h,
                    IsRuinPillar = true, Angle = a, Broken = r.NextDouble() < 0.3,
                    Outline = new[] { -h, -h, h, -h, h, h, -h, h },
                });
            }
        }
    }
}
