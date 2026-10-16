using ChronoClash.Core.Entities;

namespace ChronoClash.Core.Simulation;

/// <summary>
/// Grille de rangement des chefs et des troupes, reconstruite à chaque tick, pour trouver vite
/// « qui est près d'ici » (ramassage des bulles, séparation des troupes, et plus tard combat)
/// sans comparer chaque troupe avec toutes les autres.
/// </summary>
public sealed class SpatialGrid
{
    /// <summary>Taille d'une case de rangement (pixels). À peu près la portée d'une interaction de corps à corps.</summary>
    public const int CellSize = 48;

    private readonly Dictionary<long, List<IBody>> _cells = new();
    private readonly List<List<IBody>> _usedLists = new();

    private static long Key(int cx, int cy) => ((long)cx << 32) ^ (uint)cy;

    /// <summary>Vide la grille (les listes sont gardées pour être réutilisées, sans allocation).</summary>
    public void Clear()
    {
        foreach (var list in _usedLists) list.Clear();
        _usedLists.Clear();
    }

    public void Insert(IBody body)
    {
        var key = Key((int)MathF.Floor(body.Position.X / CellSize), (int)MathF.Floor(body.Position.Y / CellSize));
        if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<IBody>();
        if (list.Count == 0) _usedLists.Add(list);
        list.Add(body);
    }

    /// <summary>Remplit <paramref name="results"/> avec les corps des cases qui touchent le cercle (à affiner par la distance).</summary>
    public void Query(float x, float y, float radius, List<IBody> results)
    {
        results.Clear();
        int x0 = (int)MathF.Floor((x - radius) / CellSize), x1 = (int)MathF.Floor((x + radius) / CellSize);
        int y0 = (int)MathF.Floor((y - radius) / CellSize), y1 = (int)MathF.Floor((y + radius) / CellSize);
        for (int cx = x0; cx <= x1; cx++)
            for (int cy = y0; cy <= y1; cy++)
                if (_cells.TryGetValue(Key(cx, cy), out var list)) results.AddRange(list);
    }

    /// <summary>Reconstruit la grille avec tous les chefs et troupes vivants.</summary>
    public void Rebuild(World world)
    {
        Clear();
        foreach (var army in world.Armies)
        {
            if (!army.Alive) continue;
            if (!army.General.Dead) Insert(army.General);
            foreach (var u in army.Units) if (!u.Dead) Insert(u);
        }
    }
}
