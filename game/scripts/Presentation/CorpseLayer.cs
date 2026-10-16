using ChronoClash.Core.Events;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Traces laissées au sol par les morts (tache sombre + éclat à la couleur du clan), qui s'effacent lentement.
/// Dessinées sous les troupes. Limitées à <see cref="MaxCorpses"/> pour ne pas ralentir le jeu.
/// </summary>
public partial class CorpseLayer : Node2D
{
    private const int MaxCorpses = 600;
    /// <summary>Durée de vie d'une trace au sol (secondes).</summary>
    private const float Lifetime = 30f;

    private GameSession _session = null!;
    private readonly List<(Vector2 Position, Color Team, bool Big, float Age, float Angle)> _corpses = new();

    public void Setup(GameSession session)
    {
        _session = session;
        _session.EventRaised += OnEvent;
    }

    public override void _ExitTree() => _session.EventRaised -= OnEvent;

    private void OnEvent(GameEvent e)
    {
        if (e is not BodyDiedEvent d || Settings.GameSettings.Current.LowQuality) return;
        var army = _session.World.Armies[d.ArmyId];
        bool big = d.IsChief || d.Kind == Core.Commands.TroopKind.Tank;
        _corpses.Add((new Vector2(d.X, d.Y), CoreConvert.ColorOf(army.ColorRgb), big, 0, GD.Randf() * Mathf.Tau));
        if (_corpses.Count > MaxCorpses) _corpses.RemoveAt(0);
    }

    public override void _Process(double delta)
    {
        for (int i = _corpses.Count - 1; i >= 0; i--)
        {
            var c = _corpses[i];
            c.Age += (float)delta;
            if (c.Age > Lifetime) _corpses.RemoveAt(i); else _corpses[i] = c;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var (pos, team, big, age, angle) in _corpses)
        {
            float fade = 1 - age / Lifetime;
            float r = big ? 13 : 6;
            DrawSetTransform(pos, angle, new Vector2(1, 0.7f));
            DrawCircle(Vector2.Zero, r, new Color(0.18f, 0.1f, 0.06f, 0.35f * fade));
            DrawCircle(new Vector2(r * 0.3f, 0), r * 0.45f, new Color(team, 0.5f * fade));
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
