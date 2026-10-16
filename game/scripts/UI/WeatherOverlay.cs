using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>Orage : l'écran s'assombrit légèrement et des traits de pluie tombent, le temps que l'orage dure.</summary>
public partial class WeatherOverlay : Control
{
    private GameSession _session = null!;
    private float _time;

    public void Setup(GameSession session)
    {
        _session = session;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float storm = _session.World.StormTime;
        if (storm <= 0) return;
        float fade = Mathf.Clamp(Mathf.Min(storm / 2, (_session.World.Data.Tuning.Events.StormDuration - storm) / 2), 0, 1);
        var size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.1f, 0.14f, 0.22f, 0.28f * fade));
        var rain = new Color(0.75f, 0.82f, 0.95f, 0.35f * fade);
        for (int i = 0; i < 160; i++)
        {
            // Position pseudo-aléatoire stable par goutte, qui défile vers le bas.
            float x = (i * 137.5f) % size.X, y = ((i * 89.3f) + _time * 900) % (size.Y + 40) - 20;
            DrawLine(new Vector2(x, y), new Vector2(x - 6, y + 18), rain, 1.2f);
        }
    }
}
