using ChronoClash.Core.Map;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Affiche le terrain par morceaux de 512 × 512 pixels, générés à la demande autour de la caméra.
/// Un morceau généré est gardé en mémoire ; les plus anciens sont oubliés au-delà de <see cref="MaxCachedChunks"/>.
/// Pour éviter les saccades, on ne génère qu'<see cref="ChunksPerFrame"/> morceau(x) par image.
/// </summary>
public partial class MapRenderer : Node2D
{
    /// <summary>Nombre maximal de morceaux gardés en mémoire (environ 1 Mo chacun).</summary>
    private const int MaxCachedChunks = 110;
    /// <summary>Morceaux générés par image (augmenter si l'ordinateur est puissant, diminuer s'il saccade).</summary>
    private const int ChunksPerFrame = 1;
    private const int ChunksPerSide = Terrain.WorldSize / ChunkPainter.Size;
    private static readonly Color Placeholder = new(0.44f, 0.57f, 0.27f);

    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private readonly Dictionary<int, (ImageTexture Texture, long LastUsed)> _cache = new();
    private readonly List<(int Cx, int Cy)> _visible = new();
    private long _frame;

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
        TextureFilter = TextureFilterEnum.Linear;
    }

    /// <summary>Génère tout de suite les morceaux autour d'un point (au démarrage, pour ne pas voir de cases vides).</summary>
    public void Preload(Vector2 center, int radius)
    {
        int ccx = (int)(center.X / ChunkPainter.Size), ccy = (int)(center.Y / ChunkPainter.Size);
        for (int cy = ccy - radius; cy <= ccy + radius; cy++)
            for (int cx = ccx - radius; cx <= ccx + radius; cx++)
                if (InMap(cx, cy) && !_cache.ContainsKey(Key(cx, cy))) Generate(cx, cy);
    }

    private static int Key(int cx, int cy) => cy * ChunksPerSide + cx;
    private static bool InMap(int cx, int cy) => cx >= 0 && cy >= 0 && cx < ChunksPerSide && cy < ChunksPerSide;

    private void Generate(int cx, int cy)
    {
        var pixels = ChunkPainter.Paint(_session.World.Terrain, cx, cy);
        var image = Image.CreateFromData(ChunkPainter.Size, ChunkPainter.Size, false, Image.Format.Rgba8, pixels);
        _cache[Key(cx, cy)] = (ImageTexture.CreateFromImage(image), _frame);
        if (_cache.Count > MaxCachedChunks) EvictOldest();
    }

    private void EvictOldest()
    {
        int oldestKey = -1; long oldest = long.MaxValue;
        foreach (var (key, entry) in _cache)
            if (entry.LastUsed < oldest) { oldest = entry.LastUsed; oldestKey = key; }
        if (oldestKey >= 0) _cache.Remove(oldestKey);
    }

    public override void _Process(double delta)
    {
        _frame++;
        // Zone du monde visible à l'écran.
        var size = GetViewportRect().Size / _camera.Zoom;
        var center = _camera.GetScreenCenterPosition();
        var rect = new Rect2(center - size / 2, size);
        int cx0 = Math.Max(0, (int)Math.Floor(rect.Position.X / ChunkPainter.Size)), cx1 = Math.Min(ChunksPerSide - 1, (int)Math.Floor(rect.End.X / ChunkPainter.Size));
        int cy0 = Math.Max(0, (int)Math.Floor(rect.Position.Y / ChunkPainter.Size)), cy1 = Math.Min(ChunksPerSide - 1, (int)Math.Floor(rect.End.Y / ChunkPainter.Size));

        _visible.Clear();
        for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++) _visible.Add((cx, cy));
        // On génère d'abord les morceaux les plus proches du centre de l'écran.
        _visible.Sort((a, b) => DistanceToCenter(a, center).CompareTo(DistanceToCenter(b, center)));
        int generated = 0;
        foreach (var (cx, cy) in _visible)
        {
            if (_cache.ContainsKey(Key(cx, cy)) || generated >= ChunksPerFrame) continue;
            Generate(cx, cy);
            generated++;
        }
        QueueRedraw();
    }

    private static float DistanceToCenter((int Cx, int Cy) c, Vector2 center)
        => new Vector2((c.Cx + 0.5f) * ChunkPainter.Size, (c.Cy + 0.5f) * ChunkPainter.Size).DistanceSquaredTo(center);

    public override void _Draw()
    {
        foreach (var (cx, cy) in _visible)
        {
            var rect = new Rect2(cx * ChunkPainter.Size, cy * ChunkPainter.Size, ChunkPainter.Size + 0.5f, ChunkPainter.Size + 0.5f);
            if (_cache.TryGetValue(Key(cx, cy), out var entry))
            {
                DrawTextureRect(entry.Texture, rect, false);
                _cache[Key(cx, cy)] = (entry.Texture, _frame);
            }
            else DrawRect(rect, Placeholder);
        }
        // Bord de la carte.
        DrawRect(new Rect2(0, 0, Terrain.WorldSize, Terrain.WorldSize), new Color(1, 1, 1, 0.18f), false, 4 / _camera.Zoom.X);
    }
}
