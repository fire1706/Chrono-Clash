using ChronoClash.Core.Events;
using ChronoClash.Game.Localization;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Transition d'ère : quand ton clan change d'époque, l'écran flashe en blanc, une onde lumineuse part du centre
/// et une grande carte « Ère romaine — −50 » s'affiche quelques secondes.
/// </summary>
public partial class EraTransition : Control
{
    private const float Duration = 3.2f;
    private GameSession _session = null!;
    private float _time = Duration;
    private string _title = "", _year = "";

    public void Setup(GameSession session)
    {
        _session = session;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        _session.EventRaised += OnEvent;
    }

    public override void _ExitTree() => _session.EventRaised -= OnEvent;

    private void OnEvent(GameEvent e)
    {
        if (e is not EraStartedEvent es || es.ArmyId != _session.LocalArmy.Id) return;
        var era = _session.World.Data.Eras[es.Era];
        _title = Loc.T(era.Name);
        _year = era.Years;
        _time = 0;
    }

    public override void _Process(double delta)
    {
        if (_time >= Duration) return;
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_time >= Duration) return;
        var size = GetViewportRect().Size;
        var center = size / 2;
        // 1. Éclair blanc qui s'efface en 0,6 s.
        if (_time < 0.6f) DrawRect(new Rect2(Vector2.Zero, size), new Color(1, 0.97f, 0.85f, 0.6f * (1 - _time / 0.6f)));
        // 2. Onde lumineuse.
        if (_time < 1.2f) DrawArc(center, _time / 1.2f * size.X * 0.7f, 0, Mathf.Tau, 96, new Color(1, 0.9f, 0.6f, 1 - _time / 1.2f), 12);
        // 3. Carte titre : apparaît, reste, disparaît.
        float alpha = Mathf.Clamp(Mathf.Min((_time - 0.3f) / 0.4f, (Duration - _time) / 0.6f), 0, 1);
        if (alpha <= 0) return;
        var font = ThemeDB.FallbackFont;
        DrawRect(new Rect2(0, center.Y - 120, size.X, 110), new Color(0.08f, 0.06f, 0.04f, 0.65f * alpha));
        DrawString(font, new Vector2(0, center.Y - 58), _title, HorizontalAlignment.Center, size.X, 44, new Color(1, 0.88f, 0.5f, alpha));
        DrawString(font, new Vector2(0, center.Y - 22), _year, HorizontalAlignment.Center, size.X, 22, new Color(1, 1, 1, 0.8f * alpha));
    }
}
