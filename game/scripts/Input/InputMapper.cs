using ChronoClash.Core.Commands;
using ChronoClash.Game.Net;
using ChronoClash.Game.Presentation;
using Godot;

namespace ChronoClash.Game.Input;

/// <summary>
/// Transforme le clavier et la souris en ordres (<see cref="PlayerCommand"/>) envoyés à l'hôte.
/// Il n'envoie un ordre que quand quelque chose CHANGE (pas à chaque image) pour économiser le réseau.
///
/// Déplacement : ZQSD en AZERTY / WASD en QWERTY (touches « physiques » : même position sur tous les claviers)
/// ou flèches · Clic droit = aller à un point (maintenu = suivre la souris) · Maj + clic droit = ajouter une étape ·
/// Espace maintenu = sprint · X = halte.
/// À l'étape M6, ces touches deviendront personnalisables (InputMap de Godot + écran Réglages).
/// </summary>
public partial class InputMapper : Node
{
    /// <summary>Intervalle entre deux ordres quand on maintient le clic droit (secondes).</summary>
    private const float FollowInterval = 0.14f;
    /// <summary>Distance minimale (pixels du monde) avant de renvoyer un ordre en suivant la souris.</summary>
    private const float FollowMinDistance = 20f;

    private GameSession _session = null!;
    private CameraController _camera = null!;
    private ArmyRenderer _renderer = null!;

    private Vector2 _lastDirection;
    private bool _lastSprint;
    private bool _holdingRight;
    private float _followTimer;
    private Vector2 _lastFollowTarget;

    public void Setup(GameSession session, CameraController camera, ArmyRenderer renderer)
    {
        _session = session;
        _camera = camera;
        _renderer = renderer;
    }

    private long Tick => _session.World.Tick;
    private int Player => _session.LocalPlayerId;

    public override void _Process(double delta)
    {
        // ----- Direction au clavier -----
        var dir = Vector2.Zero;
        if (Godot.Input.IsPhysicalKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) dir.X += 1;
        if (Godot.Input.IsPhysicalKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) dir.X -= 1;
        if (Godot.Input.IsPhysicalKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) dir.Y += 1;
        if (Godot.Input.IsPhysicalKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
        if (dir != _lastDirection)
        {
            _lastDirection = dir;
            var n = dir.Normalized();
            _session.Send(new DirectionCommand(Tick, Player, n.X, n.Y));
        }

        // ----- Sprint -----
        bool sprint = Godot.Input.IsPhysicalKeyPressed(Key.Space);
        if (sprint != _lastSprint)
        {
            _lastSprint = sprint;
            _session.Send(new SprintCommand(Tick, Player, sprint));
        }

        // ----- Clic droit maintenu : le chef suit la souris -----
        if (_holdingRight && !Godot.Input.IsKeyPressed(Key.Shift))
        {
            _followTimer -= (float)delta;
            if (_followTimer <= 0)
            {
                _followTimer = FollowInterval;
                var target = _camera.GetGlobalMousePosition();
                var chief = _renderer.DisplayPosition(_session.LocalArmy.General);
                if (target.DistanceTo(_lastFollowTarget) > FollowMinDistance && target.DistanceTo(chief) > FollowMinDistance)
                {
                    _lastFollowTarget = target;
                    _session.Send(new MoveToCommand(Tick, Player, target.X, target.Y, false));
                }
            }
        }
    }

    /// <summary>Le relâchement du clic droit est lu ici même si un autre élément a déjà traité l'appui.</summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false }) _holdingRight = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } click:
            {
                var target = _camera.GetGlobalMousePosition();
                _session.Send(new MoveToCommand(Tick, Player, target.X, target.Y, click.ShiftPressed));
                _renderer.AddMarker(target);
                _holdingRight = !click.ShiftPressed;
                _followTimer = 0.25f; // petit délai avant de commencer à suivre la souris
                _lastFollowTarget = target;
                GetViewport().SetInputAsHandled();
                break;
            }
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.X }:
                _session.Send(new StopCommand(Tick, Player));
                break;
        }
    }
}
