using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Petits effets visuels déclenchés par les événements de la simulation :
/// cercle qui s'agrandit quand une bulle est ramassée, et texte flottant (« +1 Guerrier · +4 ◆ ») pour le joueur.
/// </summary>
public partial class EffectsLayer : Node2D
{
    private const float FloatDuration = 1.3f, BurstDuration = 0.5f;

    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private readonly List<(Vector2 Position, string Text, Color Color, float Age)> _floats = new();
    private readonly List<(Vector2 Position, Color Color, float Radius, float Age)> _bursts = new();

    public void Setup(GameSession session, Camera2D camera)
    {
        _session = session;
        _camera = camera;
        _session.EventRaised += OnEvent;
    }

    public override void _ExitTree() => _session.EventRaised -= OnEvent;

    private void OnEvent(GameEvent e)
    {
        var data = _session.World.Data;
        switch (e)
        {
            case BubbleCollectedEvent b:
            {
                var pos = new Vector2(b.X, b.Y);
                var color = b.Kind == BubbleKind.Star ? Color.FromHtml(data.Star.Color) : Color.FromHtml(data.Troop((Core.Commands.TroopKind)(int)b.Kind).Color);
                _bursts.Add((pos, color, b.Kind == BubbleKind.Tank ? 26 : 18, 0));
                if (b.ArmyId != _session.LocalArmy.Id) break; // le texte seulement pour nos propres bulles
                string text = b.Kind == BubbleKind.Star
                    ? $"+{b.Points} ★"
                    : $"+1 {data.Stats((Core.Commands.TroopKind)(int)b.Kind, _session.LocalArmy.Era).Short} · +{b.Resources} ◆";
                _floats.Add((pos, text, color.Lightened(0.3f), 0));
                break;
            }
            case TroopRecruitedEvent r when r.ArmyId == _session.LocalArmy.Id:
            {
                var g = _session.LocalArmy.General.Position.ToGodot();
                _floats.Add((g + new Vector2(0, -40), $"+1 {data.Stats(r.Kind, _session.LocalArmy.Era).Short}", Color.FromHtml(data.Troop(r.Kind).Color).Lightened(0.3f), 0));
                break;
            }
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _floats.Count - 1; i >= 0; i--)
        {
            var f = _floats[i]; f.Age += dt;
            if (f.Age > FloatDuration) _floats.RemoveAt(i); else _floats[i] = f;
        }
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            var b = _bursts[i]; b.Age += dt;
            if (b.Age > BurstDuration) _bursts.RemoveAt(i); else _bursts[i] = b;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float zoom = _camera.Zoom.X;
        foreach (var (pos, color, radius, age) in _bursts)
        {
            float q = age / BurstDuration;
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            DrawArc(pos, radius * (0.6f + q), 0, Mathf.Tau, 24, new Color(color, 1 - q), 3 * (1 - q) + 0.5f);
        }
        var font = ThemeDB.FallbackFont;
        foreach (var (pos, text, color, age) in _floats)
        {
            float q = age / FloatDuration;
            // Texte toujours à la même taille à l'écran, qui monte et s'efface.
            DrawSetTransform(pos + new Vector2(0, -20 - q * 40 / zoom), 0, new Vector2(1 / zoom, 1 / zoom));
            var c = new Color(color, 1 - q * q);
            DrawString(font, new Vector2(-150, 0), text, HorizontalAlignment.Center, 300, 15, new Color(0, 0, 0, c.A * 0.7f));
            DrawString(font, new Vector2(-151, -1), text, HorizontalAlignment.Center, 300, 15, c);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
