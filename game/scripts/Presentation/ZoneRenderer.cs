using ChronoClash.Core.Map;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// La zone battle royale : l'extérieur est teinté de violet, le bord est un trait lumineux,
/// et la prochaine zone est un cercle en pointillés blancs.
/// </summary>
public partial class ZoneRenderer : Node2D
{
    private static readonly Color Outside = new(0.35f, 0.12f, 0.55f, 0.28f);
    private static readonly Color Edge = new(0.82f, 0.55f, 1f, 0.95f);

    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private float _time;

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var z = _session.World.Zone;
        float zoom = _camera.Zoom.X;
        var center = z.Center.ToGodot();
        if (z.Radius < Terrain.WorldSize * 1.5f)
        {
            // L'extérieur : un anneau très épais autour de la zone (assez large pour couvrir l'écran et la carte).
            const float band = 16000;
            DrawArc(center, z.Radius + band / 2, 0, Mathf.Tau, 180, Outside, band);
            float pulse = 1 + Mathf.Sin(_time * 3) * 0.15f;
            if (z.Radius > 1) DrawArc(center, z.Radius, 0, Mathf.Tau, 180, Edge, 5 * pulse / zoom);
        }
        if (z.NextCenter is { } nc)
        {
            var next = nc.ToGodot();
            const int dashes = 90;
            for (int i = 0; i < dashes; i += 2)
                DrawArc(next, z.NextRadius, i * Mathf.Tau / dashes, (i + 1) * Mathf.Tau / dashes, 6, new Color(1, 1, 1, 0.7f), 3 / zoom);
        }
    }
}
