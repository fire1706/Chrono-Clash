using ChronoClash.Core.Map;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Presentation;

/// <summary>
/// Caméra du joueur : suit son chef (verrouillée) ou se déplace librement.
/// Molette = zoom vers le curseur · Y = verrouiller/libérer · C = recentrer · clic molette glissé = déplacer la vue ·
/// souris au bord de l'écran = défilement (caméra libre).
/// </summary>
public partial class CameraController : Camera2D
{
    private const float MinZoom = 0.35f, MaxZoom = 2.2f, ZoomStep = 1.12f;
    /// <summary>Vitesse de suivi du chef (plus grand = plus collé).</summary>
    private const float FollowSharpness = 7f;
    /// <summary>Zone du bord de l'écran (pixels) qui fait défiler la caméra libre, et vitesse.</summary>
    private const float EdgeSize = 14f, EdgeSpeed = 950f;

    private GameSession _session = null!;
    private ArmyRenderer _renderer = null!;
    private bool _dragging;

    /// <summary>Vrai quand la caméra suit le chef.</summary>
    public bool Locked { get; private set; } = true;

    /// <summary>Message court affiché par l'interface quand l'état de la caméra change.</summary>
    public event Action<string>? Notify;

    public void Setup(GameSession session, ArmyRenderer renderer)
    {
        _session = session;
        _renderer = renderer;
        Position = session.LocalArmy.General.Position.ToGodot();
        MakeCurrent();
    }

    /// <summary>Libère la caméra et la place sur un point (utilisé par la minicarte).</summary>
    public void FocusOn(Vector2 worldPosition)
    {
        Locked = false;
        Position = worldPosition;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var general = _session.LocalArmy.General;
        if (Locked)
        {
            // On vise un peu devant le chef quand il bouge, pour mieux voir où il va.
            var target = _renderer.DisplayPosition(general) + general.Velocity.ToGodot() * 0.25f;
            Position = Position.Lerp(target, Mathf.Min(1, FollowSharpness * dt));
        }
        else if (!_dragging)
        {
            var mouse = GetViewport().GetMousePosition();
            var size = GetViewportRect().Size;
            float step = EdgeSpeed / Zoom.X * dt;
            if (mouse.X >= 0 && mouse.Y >= 0 && mouse.X <= size.X && mouse.Y <= size.Y)
            {
                if (mouse.X < EdgeSize) Position += new Vector2(-step, 0);
                if (mouse.X > size.X - EdgeSize) Position += new Vector2(step, 0);
                if (mouse.Y < EdgeSize) Position += new Vector2(0, -step);
                if (mouse.Y > size.Y - EdgeSize) Position += new Vector2(0, step);
            }
        }
        Position = Position.Clamp(new Vector2(-200, -200), new Vector2(Terrain.WorldSize + 200, Terrain.WorldSize + 200));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } wheelUp:
                ZoomAt(wheelUp.Position, ZoomStep); GetViewport().SetInputAsHandled(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } wheelDown:
                ZoomAt(wheelDown.Position, 1 / ZoomStep); GetViewport().SetInputAsHandled(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle:
                _dragging = middle.Pressed;
                if (middle.Pressed && Locked) { Locked = false; Notify?.Invoke("Caméra libre"); }
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion motion when _dragging:
                Position -= motion.Relative / Zoom.X;
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                // Touches « logiques » : la lettre affichée sur le clavier (fonctionne en AZERTY comme en QWERTY).
                if (key.Keycode == Key.Y) { Locked = !Locked; Notify?.Invoke(Locked ? "Caméra verrouillée sur le chef" : "Caméra libre"); }
                else if (key.Keycode == Key.C) { Locked = true; Position = _renderer.DisplayPosition(_session.LocalArmy.General); Notify?.Invoke("Caméra recentrée"); }
                break;
        }
    }

    /// <summary>Zoome en gardant le point sous la souris immobile (quand la caméra est libre).</summary>
    private void ZoomAt(Vector2 screenPosition, float factor)
    {
        var viewportCenter = GetViewportRect().Size / 2;
        var before = Position + (screenPosition - viewportCenter) / Zoom.X;
        float z = Mathf.Clamp(Zoom.X * factor, MinZoom, MaxZoom);
        Zoom = new Vector2(z, z);
        if (!Locked)
        {
            var after = Position + (screenPosition - viewportCenter) / z;
            Position += before - after;
        }
    }
}
