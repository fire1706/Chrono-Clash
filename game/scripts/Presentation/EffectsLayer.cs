using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Petits effets visuels déclenchés par les événements de la simulation : cercles (ramassage, cri de guerre),
/// étincelles au corps à corps, explosions, et textes flottants pour le joueur (« +1 Guerrier · +4 ◆ »,
/// « +4 ★ ×2 ! », « Vétéran ★★ »…).
/// </summary>
public partial class EffectsLayer : Node2D
{
    private const float FloatDuration = 1.3f, BurstDuration = 0.5f;

    private GameSession _session = null!;
    private Camera2D _camera = null!;
    private readonly List<(Vector2 Position, string Text, Color Color, float Age)> _floats = new();
    private readonly List<(Vector2 Position, Color Color, float Radius, float Age)> _bursts = new();
    /// <summary>Explosions (dégâts de zone) : position, rayon, âge, plasma ou non.</summary>
    private readonly List<(Vector2 Position, float Radius, float Age, bool Plasma)> _explosions = new();
    /// <summary>Étincelles de corps à corps (limitées pour ne pas ralentir les grosses batailles).</summary>
    private readonly List<(Vector2 Position, float Age)> _sparks = new();
    private const float ExplosionDuration = 0.6f, SparkDuration = 0.18f;
    private static readonly Color StarGold = new(0.95f, 0.79f, 0.3f);

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
            case BodyDiedEvent d when d.KillerArmyId == _session.LocalArmy.Id && d.Points > 0:
                _floats.Add((new Vector2(d.X, d.Y - 14), d.Multiplier > 1 ? $"+{d.Points} ★ ×{d.Multiplier} !  +{d.Resources} ◆" : $"+{d.Points} ★  +{d.Resources} ◆", StarGold, 0));
                break;
            case VeteranEvent v when v.ArmyId == _session.LocalArmy.Id:
                _floats.Add((new Vector2(v.X, v.Y - 14), "Vétéran " + new string('★', v.Level), StarGold, 0));
                break;
            case ProjectileImpactEvent p when p.Splash > 0 && _session.CanSee(new Core.Mathematics.Vec2(p.X, p.Y)):
                _explosions.Add((new Vector2(p.X, p.Y), p.Splash, 0, p.Kind == "plasma"));
                break;
            case MeleeHitEvent m when _sparks.Count < 120:
                _sparks.Add((new Vector2(m.X, m.Y) + new Vector2(GD.Randf() * 6 - 3, GD.Randf() * 6 - 3), 0));
                break;
            case TacticUsedEvent t when t.Tactic == Core.Commands.TacticKind.WarCry:
                _bursts.Add((new Vector2(t.X, t.Y), new Color(1, 0.55f, 0.15f), 300, 0));
                break;
            case TacticUsedEvent t when t.Tactic == Core.Commands.TacticKind.Aim && t.ArmyId == _session.LocalArmy.Id:
                _bursts.Add((new Vector2(t.X, t.Y), new Color(1, 0.3f, 0.25f), 60, 0));
                break;
            case MeteorImpactEvent mi:
                _explosions.Add((new Vector2(mi.X, mi.Y), 70, 0, false));
                break;
            case CrateCollectedEvent cc:
                _bursts.Add((new Vector2(cc.X, cc.Y), StarGold, 50, 0));
                if (cc.ArmyId == _session.LocalArmy.Id) _floats.Add((new Vector2(cc.X, cc.Y - 30), "+60 ◆  +40 ★  +4 troupes", StarGold, 0));
                break;
            case PortalUsedEvent pu:
                _bursts.Add((new Vector2(pu.X, pu.Y), new Color(0.71f, 0.42f, 1f), 70, 0));
                break;
            case ShrineCapturedEvent sc:
                _bursts.Add((new Vector2(sc.X, sc.Y), CoreConvert.ColorOf(_session.World.Armies[sc.ArmyId].ColorRgb), 60, 0));
                break;
            case ChiefPromotedEvent pr:
            {
                var g = _session.World.Armies[pr.ArmyId].General.Position.ToGodot();
                _bursts.Add((g, StarGold, 40, 0));
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
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            var x = _explosions[i]; x.Age += dt;
            if (x.Age > ExplosionDuration) _explosions.RemoveAt(i); else _explosions[i] = x;
        }
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var sp = _sparks[i]; sp.Age += dt;
            if (sp.Age > SparkDuration) _sparks.RemoveAt(i); else _sparks[i] = sp;
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
        foreach (var (pos, radius, age, plasma) in _explosions)
        {
            // Boule de feu qui gonfle puis s'efface, avec une onde de choc.
            float q = age / ExplosionDuration;
            var core = plasma ? new Color(0.55f, 0.9f, 1f) : new Color(1f, 0.62f, 0.2f);
            DrawCircle(pos, radius * (0.4f + q * 0.6f), new Color(core, 0.55f * (1 - q)));
            DrawCircle(pos, radius * 0.35f * (1 - q), new Color(1, 1, 0.85f, 0.8f * (1 - q)));
            DrawArc(pos, radius * (0.6f + q * 0.8f), 0, Mathf.Tau, 28, new Color(1, 1, 1, 0.5f * (1 - q)), 2);
        }
        foreach (var (pos, age) in _sparks)
        {
            float q = age / SparkDuration;
            for (int k = 0; k < 4; k++)
            {
                var d = Vector2.FromAngle(k * Mathf.Pi / 2 + 0.6f) * (3 + q * 6);
                DrawLine(pos + d * 0.4f, pos + d, new Color(1, 0.95f, 0.7f, 1 - q), 1.4f);
            }
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
