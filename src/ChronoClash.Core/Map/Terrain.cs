using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Map;

/// <summary>
/// Le terrain de la carte : altitude (eau, sable, herbe, collines), forêts, terre, rochers et ruines.
/// Il est généré par <see cref="MapGenerator"/> à partir de la graine de la partie :
/// chaque machine le recalcule elle-même, il n'est jamais envoyé en réseau.
///
/// Les valeurs sont stockées sur une grille (une case tous les <see cref="GridCell"/> pixels)
/// et lissées entre les cases (interpolation bilinéaire) quand on les lit.
/// </summary>
public sealed class Terrain
{
    // ---------- Dimensions ----------
    /// <summary>Côté de la carte en pixels du monde (carré). Multiple de 512 (taille des morceaux affichés).</summary>
    public const int WorldSize = 11264;
    /// <summary>Taille d'une case de la grille d'altitude, en pixels.</summary>
    public const int GridCell = 16;
    /// <summary>Nombre de cases par côté.</summary>
    public const int GridCount = WorldSize / GridCell;
    /// <summary>Nombre de points par côté (une case = 4 coins, d'où le +1).</summary>
    public const int GridPoints = GridCount + 1;

    // ---------- Niveaux d'altitude ----------
    /// <summary>En dessous : de l'eau. Au-dessus : la terre ferme.</summary>
    public const float WaterLevel = 0.37f;

    public Terrain(ThemeDef theme, uint seed)
    {
        Theme = theme;
        Seed = seed;
        Height = new float[GridPoints * GridPoints];
        ForestDensity = new float[GridPoints * GridPoints];
        DirtDensity = new float[GridPoints * GridPoints];
    }

    public ThemeDef Theme { get; }
    public uint Seed { get; }

    /// <summary>Altitude brute (0 à 1) pour chaque point de grille.</summary>
    internal float[] Height { get; }
    /// <summary>Densité de végétation (0 à 1) : au-dessus de Theme.ForestThreshold, c'est une forêt.</summary>
    internal float[] ForestDensity { get; }
    /// <summary>Variation de couleur du sol (herbe claire / foncée / terre).</summary>
    internal float[] DirtDensity { get; }

    /// <summary>En dessous de ce niveau, l'eau est trop profonde pour être traversée.</summary>
    public float DeepLevel => WaterLevel - Theme.DeepOffset;

    /// <summary>Au-dessus de ce niveau, on est sur une colline (calculé à la génération).</summary>
    public float HillLevel { get; internal set; } = 1f;

    /// <summary>Points de départ des clans (calculés par <see cref="SpawnPlanner"/>).</summary>
    public List<Vec2> SpawnPoints { get; } = new();

    public List<Rock> Rocks { get; } = new();
    public List<Ruin> Ruins { get; } = new();

    // ---------- Lecture des grilles ----------

    /// <summary>Lit une grille à une position du monde, avec lissage entre les 4 points voisins.</summary>
    private static float Bilinear(float[] grid, float x, float y)
    {
        float gx = MathUtil.Clamp(x / GridCell, 0, GridCount - 0.001f);
        float gy = MathUtil.Clamp(y / GridCell, 0, GridCount - 0.001f);
        int i = (int)gx, j = (int)gy;
        float fx = gx - i, fy = gy - j;
        int k = j * GridPoints + i;
        float a = grid[k], b = grid[k + 1], c = grid[k + GridPoints], d = grid[k + GridPoints + 1];
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    public float HeightAt(float x, float y) => Bilinear(Height, x, y);
    public float ForestAt(float x, float y) => Bilinear(ForestDensity, x, y);
    public float DirtAt(float x, float y) => Bilinear(DirtDensity, x, y);

    // ---------- Questions sur le terrain ----------

    /// <summary>Eau trop profonde pour passer (bloque le passage, sauf glace en hiver).</summary>
    public bool IsDeepWater(float x, float y) => HeightAt(x, y) < DeepLevel;

    /// <summary>Eau peu profonde : on peut patauger, lentement (en hiver : lac gelé).</summary>
    public bool IsShallowWater(float x, float y)
    {
        float h = HeightAt(x, y);
        return h < WaterLevel && h >= DeepLevel;
    }

    /// <summary>N'importe quelle eau (profonde ou non).</summary>
    public bool IsWater(float x, float y) => HeightAt(x, y) < WaterLevel;

    public bool IsSand(float x, float y) => HeightAt(x, y) < WaterLevel + 0.025f;
    public bool IsForest(float x, float y) => ForestAt(x, y) > Theme.ForestThreshold && HeightAt(x, y) > WaterLevel + 0.03f;
    public bool IsHill(float x, float y) => HeightAt(x, y) > HillLevel;

    /// <summary>
    /// Multiplicateur de vitesse selon le sol : eau peu profonde (lent), forêt (ralenti), sable (un peu ralenti).
    /// Les effets temporaires (orage…) seront ajoutés par les systèmes, pas ici.
    /// </summary>
    public float SpeedFactor(float x, float y)
    {
        if (IsShallowWater(x, y)) return Theme.WadeSpeed;
        float f = 1f;
        if (IsForest(x, y)) f *= 0.72f;
        if (IsSand(x, y)) f *= 0.9f;
        return f;
    }

    /// <summary>
    /// Vrai si un cercle de rayon <paramref name="radius"/> centré en (x, y) peut se tenir ici :
    /// dans la carte, pas dans l'eau profonde, pas dans un rocher.
    /// C'est LA fonction de collision utilisée par tous les déplacements.
    /// </summary>
    public bool IsWalkable(float x, float y, float radius)
    {
        if (x < radius || y < radius || x > WorldSize - radius || y > WorldSize - radius) return false;
        // On teste le centre et 4 points autour : assez précis et rapide.
        if (IsDeepWater(x, y) || IsDeepWater(x + radius, y) || IsDeepWater(x - radius, y)
            || IsDeepWater(x, y + radius) || IsDeepWater(x, y - radius)) return false;
        foreach (var rock in RocksNear(x, y))
        {
            float d = rock.Radius * 0.86f + radius;
            float dx = x - rock.X, dy = y - rock.Y;
            if (dx * dx + dy * dy < d * d) return false;
        }
        return true;
    }

    public bool IsWalkable(Vec2 p, float radius) => IsWalkable(p.X, p.Y, radius);

    /// <summary>Vrai si aucune eau n'est présente sur un cercle autour du point (utile pour choisir les points de départ).</summary>
    public bool IsOpenAround(float x, float y, float radius)
    {
        for (float a = 0; a < 6.28f; a += 0.8f)
            if (HeightAt(x + MathF.Cos(a) * radius, y + MathF.Sin(a) * radius) < WaterLevel + 0.02f) return false;
        return true;
    }

    // ---------- Rochers (rangés par zones pour aller vite) ----------

    /// <summary>Taille d'une zone de rangement des rochers.</summary>
    private const int RockBucket = 256;
    private const int RockBucketsPerSide = (WorldSize + RockBucket - 1) / RockBucket;
    private readonly Dictionary<int, List<Rock>> _rockBuckets = new();
    private static readonly List<Rock> NoRocks = new();

    /// <summary>Ajoute un rocher et le range dans toutes les zones qu'il touche.</summary>
    internal void AddRock(Rock rock)
    {
        Rocks.Add(rock);
        int x0 = (int)MathF.Floor((rock.X - rock.Radius) / RockBucket), x1 = (int)MathF.Floor((rock.X + rock.Radius) / RockBucket);
        int y0 = (int)MathF.Floor((rock.Y - rock.Radius) / RockBucket), y1 = (int)MathF.Floor((rock.Y + rock.Radius) / RockBucket);
        for (int bx = x0; bx <= x1; bx++)
            for (int by = y0; by <= y1; by++)
            {
                int key = by * RockBucketsPerSide + bx;
                if (!_rockBuckets.TryGetValue(key, out var list)) _rockBuckets[key] = list = new List<Rock>();
                list.Add(rock);
            }
    }

    /// <summary>Rochers proches d'un point (ceux de sa zone).</summary>
    public List<Rock> RocksNear(float x, float y)
    {
        int key = (int)MathF.Floor(y / RockBucket) * RockBucketsPerSide + (int)MathF.Floor(x / RockBucket);
        return _rockBuckets.TryGetValue(key, out var list) ? list : NoRocks;
    }

    // ---------- Petit hasard d'affichage ----------

    /// <summary>
    /// Nombre pseudo-aléatoire stable (0 à 1) pour une coordonnée : sert aux détails visuels
    /// (grain du sol, position des arbres) pour qu'ils soient identiques à chaque affichage.
    /// </summary>
    public float VisualHash(int x, int y)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263 + (int)Seed;
            n = (n ^ (int)((uint)n >> 13)) * 1274126177;
            return (uint)(n ^ (int)((uint)n >> 16)) / 4294967296f;
        }
    }
}
