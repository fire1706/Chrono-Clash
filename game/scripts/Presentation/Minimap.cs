using ChronoClash.Core.Commands;
using ChronoClash.Core.Map;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Minicarte en bas à gauche : terrain, chefs, chemin du joueur et zone vue par la caméra.
/// Clic gauche (ou glisser) = regarder à cet endroit · clic droit = y envoyer son chef.
/// </summary>
public partial class Minimap : Control
{
    /// <summary>Résolution de l'image de la minicarte (en pixels de texture).</summary>
    private const int Resolution = 256;
    /// <summary>Taille affichée à l'écran.</summary>
    private const float DisplaySize = 200;

    private GameSession _session = null!;
    private CameraController _camera = null!;
    private ArmyRenderer _renderer = null!;
    private ImageTexture _texture = null!;
    private bool _dragging;

    public void Setup(GameSession session, CameraController camera, ArmyRenderer renderer)
    {
        _session = session;
        _camera = camera;
        _renderer = renderer;
        Size = new Vector2(DisplaySize, DisplaySize);
        MouseFilter = MouseFilterEnum.Stop;
        _texture = BuildTexture(session.World.Terrain);
    }

    /// <summary>Peint une fois pour toutes une version réduite du terrain.</summary>
    private static ImageTexture BuildTexture(Terrain t)
    {
        var th = t.Theme;
        var buf = new byte[Resolution * Resolution * 4];
        float step = Terrain.WorldSize / (float)Resolution;
        for (int j = 0; j < Resolution; j++)
            for (int i = 0; i < Resolution; i++)
            {
                float x = (i + 0.5f) * step, y = (j + 0.5f) * step, h = t.HeightAt(x, y);
                Rgb c = h < t.DeepLevel ? th.Deep : h < Terrain.WaterLevel ? th.WetB : h < Terrain.WaterLevel + 0.022f ? th.Sand
                    : t.IsForest(x, y) ? th.Forest : t.DirtAt(x, y) > 0.5f ? th.GrassB : th.GrassA;
                int k = (j * Resolution + i) * 4;
                buf[k] = c.R; buf[k + 1] = c.G; buf[k + 2] = c.B; buf[k + 3] = 255;
            }
        // Les rochers en gris (au moins un pixel).
        foreach (var rock in t.Rocks)
        {
            int i = (int)(rock.X / step), j = (int)(rock.Y / step);
            if (i < 0 || j < 0 || i >= Resolution || j >= Resolution) continue;
            int k = (j * Resolution + i) * 4;
            buf[k] = 143; buf[k + 1] = 137; buf[k + 2] = 127;
        }
        return ImageTexture.CreateFromImage(Image.CreateFromData(Resolution, Resolution, false, Image.Format.Rgba8, buf));
    }

    private float Scale => Size.X / Terrain.WorldSize;
    private Vector2 ToMap(Vector2 world) => world * Scale;
    private Vector2 ToWorld(Vector2 local) => local / Scale;

    public override void _Process(double delta)
    {
        var viewport = GetViewportRect().Size;
        Position = new Vector2(12, viewport.Y - Size.Y - 12);
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(-4, -4, Size.X + 8, Size.Y + 8), new Color(0.12f, 0.1f, 0.09f, 0.9f));
        DrawTextureRect(_texture, new Rect2(Vector2.Zero, Size), false);

        // Chemin du joueur.
        var g = _session.LocalArmy.General;
        if (g.Path.Count > 0)
        {
            var from = ToMap(_renderer.DisplayPosition(g));
            foreach (var p in g.Path) { var to = ToMap(p.ToGodot()); DrawDashedLine(from, to, new Color(1, 1, 1, 0.85f), 1, 4); from = to; }
        }

        // Zone actuelle (trait violet) et prochaine zone (pointillés blancs).
        var world = _session.World;
        var z = world.Zone;
        if (z.Radius < Terrain.WorldSize)
            DrawArc(ToMap(z.Center.ToGodot()), z.Radius * Scale, 0, Mathf.Tau, 64, new Color(0.82f, 0.55f, 1f), 2);
        if (z.NextCenter is { } nc)
            for (int i = 0; i < 32; i += 2)
                DrawArc(ToMap(nc.ToGodot()), z.NextRadius * Scale, i * Mathf.Tau / 32, (i + 1) * Mathf.Tau / 32, 4, Colors.White, 1.2f);
        // Objectifs : sanctuaires (losanges à la couleur du propriétaire), portails, largage, boss.
        foreach (var s in world.Shrines)
        {
            var c = ToMap(s.Position.ToGodot());
            var col = s.Owner != null ? CoreConvert.ColorOf(s.Owner.ColorRgb) : new Color(0.85f, 0.8f, 0.7f);
            DrawColoredPolygon(new[] { c + new Vector2(0, -4), c + new Vector2(4, 0), c + new Vector2(0, 4), c + new Vector2(-4, 0) }, col);
        }
        foreach (var portal in world.Portals) DrawCircle(ToMap(portal.Position.ToGodot()), 3.2f, new Color(0.71f, 0.42f, 1f, 0.85f));
        if (world.Crate is { } crate) DrawRect(new Rect2(ToMap(crate.Position.ToGodot()) - new Vector2(3, 3), new Vector2(6, 6)), new Color(0.95f, 0.79f, 0.3f));
        if (world.Boss is { Dead: false } boss) DrawCircle(ToMap(boss.Position.ToGodot()), 4.5f + Mathf.Sin((float)Time.GetTicksMsec() / 200f), new Color(1, 0.35f, 0.27f));

        // Zone visible par la caméra.
        var viewSize = GetViewportRect().Size / _camera.Zoom;
        var center = _camera.GetScreenCenterPosition();
        DrawRect(new Rect2(ToMap(center - viewSize / 2), viewSize * Scale), new Color(1, 1, 1, 0.9f), false, 1.5f);

        // Bulles (les étoiles et les tanks un peu plus gros).
        var data = _session.World.Data;
        foreach (var b in _session.World.Bubbles)
        {
            var color = b.Kind == Core.Entities.BubbleKind.Star ? Color.FromHtml(data.Star.Color) : Color.FromHtml(data.Troop((Core.Commands.TroopKind)(int)b.Kind).Color);
            float size = b.Kind is Core.Entities.BubbleKind.Star or Core.Entities.BubbleKind.Tank ? 2.2f : 1.3f;
            DrawRect(new Rect2(ToMap(b.Position.ToGodot()) - new Vector2(size / 2, size / 2), new Vector2(size, size)), color);
        }

        // Troupes (petits points à la couleur du clan).
        foreach (var army in _session.World.Armies)
        {
            if (!army.Alive) continue;
            var team = CoreConvert.ColorOf(army.ColorRgb);
            bool mine = army == _session.LocalArmy;
            foreach (var u in army.Units)
                if (!u.Dead && (mine || _session.CanSee(u.Position))) DrawRect(new Rect2(ToMap(u.Position.ToGodot()) - Vector2.One, new Vector2(2, 2)), team);
        }

        // Chefs.
        foreach (var army in _session.World.Armies)
        {
            if (!army.Alive || army.General.Dead) continue;
            if (army != _session.LocalArmy && !_session.CanSee(army.General.Position)) continue;
            var p = ToMap(_renderer.DisplayPosition(army.General));
            DrawCircle(p, 5, new Color(0.1f, 0.07f, 0.04f));
            DrawCircle(p, 4, CoreConvert.ColorOf(army.ColorRgb));
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } left:
                _dragging = left.Pressed;
                if (left.Pressed) _camera.FocusOn(ToWorld(left.Position));
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                _camera.FocusOn(ToWorld(motion.Position));
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } right:
            {
                var w = ToWorld(right.Position);
                _session.Send(new MoveToCommand(_session.World.Tick, _session.LocalPlayerId, w.X, w.Y, right.ShiftPressed));
                _renderer.AddMarker(w);
                AcceptEvent();
                break;
            }
        }
    }
}
