namespace ChronoClash.Core.Map;

/// <summary>
/// Grille de navigation grossière (une case tous les 32 pixels) : chaque case est marquée
/// « bloquée » si un chef ne peut pas s'y tenir. Le <see cref="Pathfinder"/> cherche ses chemins dessus.
/// </summary>
public sealed class NavGrid
{
    /// <summary>Taille d'une case de navigation, en pixels.</summary>
    public const int Cell = 32;
    public const int Count = Terrain.WorldSize / Cell;
    /// <summary>Rayon de test d'une case : un peu plus que le rayon d'un chef, pour garder une marge.</summary>
    private const float TestRadius = 15;

    private readonly byte[] _blocked = new byte[Count * Count];

    public NavGrid(Terrain terrain)
    {
        for (int j = 0; j < Count; j++)
            for (int i = 0; i < Count; i++)
                _blocked[j * Count + i] = terrain.IsWalkable(i * Cell + Cell / 2f, j * Cell + Cell / 2f, TestRadius) ? (byte)0 : (byte)1;
    }

    public bool IsBlocked(int cell) => _blocked[cell] != 0;

    /// <summary>Numéro de la case qui contient un point du monde.</summary>
    public static int CellOf(float x, float y)
    {
        int i = Math.Clamp((int)MathF.Floor(x / Cell), 0, Count - 1);
        int j = Math.Clamp((int)MathF.Floor(y / Cell), 0, Count - 1);
        return j * Count + i;
    }

    /// <summary>Centre d'une case, en pixels du monde.</summary>
    public static (float X, float Y) CenterOf(int cell) => ((cell % Count) * Cell + Cell / 2f, (cell / Count) * Cell + Cell / 2f);

    /// <summary>Case libre la plus proche (en cherchant en carrés de plus en plus grands), ou -1.</summary>
    public int NearestOpen(int cell)
    {
        int ci = cell % Count, cj = cell / Count;
        for (int r = 1; r < 14; r++)
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    if (Math.Max(Math.Abs(di), Math.Abs(dj)) != r) continue; // seulement le bord du carré
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= Count || j >= Count) continue;
                    if (_blocked[j * Count + i] == 0) return j * Count + i;
                }
        return -1;
    }
}
