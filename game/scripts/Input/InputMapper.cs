using ChronoClash.Core.Commands;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using ChronoClash.Game.Presentation;
using ChronoClash.Game.UI;
using Godot;

namespace ChronoClash.Game.Input;

/// <summary>
/// Transforme le clavier et la souris en ordres (<see cref="PlayerCommand"/>) envoyés à l'hôte.
/// Il n'envoie un ordre que quand quelque chose CHANGE (pas à chaque image) pour économiser le réseau.
///
/// Déplacement : ZQSD en AZERTY / WASD en QWERTY (touches « physiques » : même position sur tous les claviers)
/// ou flèches · Clic droit = aller à un point (maintenu = suivre la souris) · Maj + clic droit = ajouter une étape ·
/// Espace maintenu = sprint · X = halte · 1 charge · 2 retrait · 3 viser (le curseur) · R cri de guerre ·
/// 4/5/6 = acheter guerrier/distance/tank · Tab = formation suivante · 7/8/9/0 = choisir une formation ·
/// F = éditeur de formation.
/// Tests : F4 = +20 troupes · F6 = clan ennemi immobile (Maj + F6 : 2 ères d'avance) · F7 = +50 ★.
/// À l'étape M6, ces touches deviendront personnalisables (écran Réglages).
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
    private FormationEditor _editor = null!;
    private FormationStore _store = null!;
    private GameHud _hud = null!;

    private Vector2 _lastDirection;
    private bool _lastSprint;
    private bool _holdingRight;
    private float _followTimer;
    private Vector2 _lastFollowTarget;

    public void Setup(GameSession session, CameraController camera, ArmyRenderer renderer, FormationEditor editor, FormationStore store, GameHud hud)
    {
        _session = session;
        _camera = camera;
        _renderer = renderer;
        _editor = editor;
        _store = store;
        _hud = hud;
    }

    private long Tick => _session.World.Tick;
    private int Player => _session.LocalPlayerId;

    public override void _Process(double delta)
    {
        // ----- Direction au clavier (bloquée pendant l'édition de formation) -----
        var dir = Vector2.Zero;
        if (!_editor.IsOpen)
        {
            if (Godot.Input.IsPhysicalKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) dir.X += 1;
            if (Godot.Input.IsPhysicalKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) dir.X -= 1;
            if (Godot.Input.IsPhysicalKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) dir.Y += 1;
            if (Godot.Input.IsPhysicalKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) dir.Y -= 1;
        }
        if (dir != _lastDirection)
        {
            _lastDirection = dir;
            var n = dir.Normalized();
            _session.Send(new DirectionCommand(Tick, Player, n.X, n.Y));
        }

        // ----- Sprint -----
        bool sprint = !_editor.IsOpen && Godot.Input.IsPhysicalKeyPressed(Key.Space);
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
        if (_editor.IsOpen) return;
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
            case InputEventKey { Pressed: true } key:
                OnKey(key);
                break;
        }
    }

    private void OnKey(InputEventKey key)
    {
        // Achats : la répétition (touche maintenue) est autorisée pour acheter vite.
        switch (key.PhysicalKeycode)
        {
            case Key.Key4: Buy(TroopKind.Warrior); return;
            case Key.Key5: Buy(TroopKind.Ranged); return;
            case Key.Key6: Buy(TroopKind.Tank); return;
        }
        if (key.Echo) return;
        switch (key.PhysicalKeycode)
        {
            case Key.Key1: Tactic(TacticKind.Charge); return;
            case Key.Key2: Tactic(TacticKind.Retreat); return;
            case Key.Key3: Tactic(TacticKind.Aim); return;
            case Key.Key7: SelectFormation(0); return;
            case Key.Key8: SelectFormation(1); return;
            case Key.Key9: SelectFormation(2); return;
            case Key.Key0: SelectFormation(3); return;
            case Key.Tab: SelectFormation((_store.Current + (key.ShiftPressed ? 3 : 1)) % 4); GetViewport().SetInputAsHandled(); return;
        }
        switch (key.Keycode)
        {
            case Key.X: _session.Send(new StopCommand(Tick, Player)); break;
            case Key.R: Tactic(TacticKind.WarCry); break;
            case Key.F6:
            {
                int count = _session.World.Data.Tuning.Debug.EnemyTroopsCount;
                _session.Send(new DebugSpawnEnemyCommand(Tick, Player, count, key.ShiftPressed ? 2 : 0));
                _hud.ShowChip(key.ShiftPressed ? "Test : clan ennemi avec 2 ères d'avance" : "Test : clan ennemi devant toi");
                break;
            }
            case Key.F7:
                _session.Send(new DebugAddPointsCommand(Tick, Player, _session.World.Data.Tuning.Debug.AddPoints));
                _hud.ShowChip($"Test : +{_session.World.Data.Tuning.Debug.AddPoints} ★");
                break;
            case Key.F: _editor.Open(); break;
            case Key.F4:
                _session.Send(new DebugSpawnTroopsCommand(Tick, Player, _session.World.Data.Tuning.Debug.SpawnTroopsCount));
                _hud.ShowChip("Test : +20 troupes");
                break;
        }
    }

    private void Buy(TroopKind kind) => _session.Send(new BuyCommand(Tick, Player, kind));

    /// <summary>Lance une tactique. « Viser » prend le point du monde sous le curseur.</summary>
    private void Tactic(TacticKind kind)
    {
        var aim = _camera.GetGlobalMousePosition();
        _session.Send(new TacticCommand(Tick, Player, kind, aim.X, aim.Y));
    }

    private void SelectFormation(int index)
    {
        _store.Current = index;
        _store.Save();
        _session.Send(new SelectFormationCommand(Tick, Player, index));
        _hud.ShowChip($"Formation : {_store.Names[index]}");
    }
}
