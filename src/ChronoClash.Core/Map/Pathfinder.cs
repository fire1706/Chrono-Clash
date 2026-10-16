using ChronoClash.Core.Mathematics;

namespace ChronoClash.Core.Map;

/// <summary>
/// Calcul de chemins : A* sur la <see cref="NavGrid"/>, puis lissage pour supprimer les zigzags.
/// Les tableaux de travail sont réutilisés d'un appel à l'autre (grâce à un « tampon » numéroté)
/// pour ne pas allouer de mémoire à chaque recherche.
/// </summary>
public sealed class Pathfinder
{
    /// <summary>Nombre maximal de cases explorées par recherche (évite les gels sur les chemins impossibles).</summary>
    private const int MaxIterations = 90000;
    /// <summary>Pas de vérification d'une ligne droite, en pixels.</summary>
    private const float LineStep = 10;

    private readonly Terrain _terrain;
    private readonly NavGrid _nav;
    private readonly float[] _cost = new float[NavGrid.Count * NavGrid.Count];
    private readonly int[] _from = new int[NavGrid.Count * NavGrid.Count];
    private readonly uint[] _seen = new uint[NavGrid.Count * NavGrid.Count];
    private readonly uint[] _closed = new uint[NavGrid.Count * NavGrid.Count];
    private readonly PriorityQueue<int, float> _open = new();
    private uint _stamp;

    public Pathfinder(Terrain terrain, NavGrid nav)
    {
        _terrain = terrain;
        _nav = nav;
    }

    /// <summary>
    /// Chemin de <paramref name="from"/> à <paramref name="to"/> pour une entité de rayon <paramref name="radius"/>.
    /// Renvoie la liste des étapes (sans le point de départ), ou null si c'est impossible.
    /// Si la ligne droite est libre, le chemin est simplement [to].
    /// </summary>
    public List<Vec2>? Plan(Vec2 from, Vec2 to, float radius = 13)
    {
        if (_terrain.IsWalkable(to, radius + 1) && LineClear(from, to, radius)) return new List<Vec2> { to };
        var raw = AStar(from, to);
        return raw == null ? null : Smooth(from, raw, radius);
    }

    /// <summary>Vrai si on peut aller tout droit de a à b sans rien heurter.</summary>
    public bool LineClear(Vec2 a, Vec2 b, float radius = 13)
    {
        float d = Vec2.Distance(a, b);
        int n = (int)MathF.Ceiling(d / LineStep);
        for (int k = 1; k <= n; k++)
        {
            float t = k / (float)n;
            if (!_terrain.IsWalkable(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, radius)) return false;
        }
        return true;
    }

    /// <summary>Supprime les étapes inutiles : depuis chaque point, on saute à l'étape la plus lointaine visible en ligne droite.</summary>
    private List<Vec2> Smooth(Vec2 start, List<Vec2> points, float radius)
    {
        var result = new List<Vec2>();
        var current = start;
        int i = 0;
        while (i < points.Count)
        {
            int j = points.Count - 1;
            while (j > i && !LineClear(current, points[j], radius)) j--;
            result.Add(points[j]);
            current = points[j];
            i = j + 1;
        }
        return result;
    }

    /// <summary>A* classique en 8 directions, sans couper les coins.</summary>
    private List<Vec2>? AStar(Vec2 fromPos, Vec2 toPos)
    {
        const int N = NavGrid.Count;
        int start = NavGrid.CellOf(fromPos.X, fromPos.Y), target = NavGrid.CellOf(toPos.X, toPos.Y);
        bool exact = true;
        if (_nav.IsBlocked(target)) { target = _nav.NearestOpen(target); exact = false; if (target < 0) return null; }
        if (_nav.IsBlocked(start)) { start = _nav.NearestOpen(start); if (start < 0) return null; }

        uint stamp = ++_stamp; // un nouveau numéro rend les anciennes valeurs des tableaux invalides
        int ti = target % N, tj = target / N;
        float Heuristic(int c)
        {
            int dx = Math.Abs(c % N - ti), dy = Math.Abs(c / N - tj);
            return (dx + dy) + (1.4142f - 2f) * Math.Min(dx, dy); // distance « octile »
        }
        float CostOf(int c) => _seen[c] == stamp ? _cost[c] : float.MaxValue;

        _open.Clear();
        _seen[start] = stamp; _cost[start] = 0; _from[start] = -1;
        _open.Enqueue(start, Heuristic(start));
        bool found = false;
        for (int it = 0; it < MaxIterations && _open.Count > 0; it++)
        {
            int c = _open.Dequeue();
            if (_closed[c] == stamp) continue;
            _closed[c] = stamp;
            if (c == target) { found = true; break; }
            int ci = c % N, cj = c / N;
            float gc = _cost[c];
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= N || j >= N) continue;
                    int n = j * N + i;
                    if (_nav.IsBlocked(n) || _closed[n] == stamp) continue;
                    // Diagonale interdite si un des deux côtés est bloqué (on ne coupe pas les coins des rochers).
                    if (di != 0 && dj != 0 && (_nav.IsBlocked(cj * N + i) || _nav.IsBlocked(j * N + ci))) continue;
                    float ng = gc + (di != 0 && dj != 0 ? 1.4142f : 1f);
                    if (ng < CostOf(n))
                    {
                        _seen[n] = stamp; _cost[n] = ng; _from[n] = c;
                        _open.Enqueue(n, ng + Heuristic(n));
                    }
                }
        }
        if (!found) return null;

        // On remonte le chemin depuis la cible.
        var points = new List<Vec2>();
        for (int c = target; c != -1 && c != start; c = _from[c])
        {
            var (x, y) = NavGrid.CenterOf(c);
            points.Add(new Vec2(x, y));
        }
        points.Reverse();
        if (exact)
        {
            if (points.Count > 0) points[^1] = toPos; else points.Add(toPos);
        }
        return points;
    }
}
