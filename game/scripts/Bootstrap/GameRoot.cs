using ChronoClash.Core.Simulation;
using ChronoClash.Game.Audio;
using ChronoClash.Game.Input;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using ChronoClash.Game.Presentation;
using ChronoClash.Game.Settings;
using ChronoClash.Game.Stats;
using ChronoClash.Game.Tutorial;
using ChronoClash.Game.UI;
using Godot;

namespace ChronoClash.Game.Bootstrap;

/// <summary>
/// Point d'entrée du jeu. Au lancement : l'écran titre (<see cref="TitleScreen"/>).
/// Quand la partie commence (solo, tutoriel, hébergée ou rejointe), on assemble l'affichage du monde.
/// L'ordre d'ajout compte : la session (simulation) d'abord, puis la carte, les bulles, les troupes, les chefs,
/// les effets, la caméra, les contrôles et enfin l'interface par-dessus.
///
/// Touches : Échap = pause · classement (L par défaut) · F2 = nouvelle partie solo (thème suivant) ou retour au menu ·
/// F3 = panneau technique · en solo : F8 mode, F9 difficulté, F10 brouillard.
/// </summary>
public partial class GameRoot : Node2D
{
    /// <summary>Relancer directement une partie solo après un rechargement (Rejouer, F2, F8, F9, F10).</summary>
    private static MatchSettings? _autoSolo;

    private GameSession _session = null!;
    private PlayerProfile _profile = null!;
    private CanvasLayer? _titleLayer;
    private DebugHud? _debug;
    private Leaderboard? _board;
    private PauseMenu? _pause;
    private FormationEditor? _editor;

    public override void _Ready()
    {
        if (DedicatedServer.IsRequested())
        {
            // Lancé en serveur dédié (sans affichage) : pas de menus, pas de rendu.
            AddChild(new DedicatedServer { Name = "DedicatedServer" });
            return;
        }
        _ = GameSettings.Current; // charge et applique les réglages (langue, touches, volumes)
        _profile = PlayerProfile.Load();
        _session = new GameSession { Name = "Session" };
        AddChild(_session);
        _session.MatchStarted += () => Callable.From(BuildGame).CallDeferred();

        if (_autoSolo is { } settings)
        {
            _autoSolo = null;
            _session.StartSolo(settings, _profile.Name, _profile.ColorRgb, _profile.Emblem);
            return;
        }
        _titleLayer = new CanvasLayer { Name = "Title" };
        AddChild(_titleLayer);
        var title = new TitleScreen { Name = "TitleScreen" };
        _titleLayer.AddChild(title);
        title.Setup(_session, _profile);
    }

    /// <summary>Assemble l'affichage de la partie (appelé une fois la partie lancée).</summary>
    private void BuildGame()
    {
        _titleLayer?.QueueFree();
        _titleLayer = null;
        var session = _session;
        var store = FormationStore.Load();
        session.SendFormations(store);

        // ---- Monde (du plus bas au plus haut à l'écran) ----
        var map = new MapRenderer { Name = "Map", ZIndex = -10 };
        var bubbles = new BubbleRenderer { Name = "Bubbles", ZIndex = 0 };
        var ground = new ObjectiveRenderer { Name = "GroundObjectives", ZIndex = 1, Overlay = false };
        var corpses = new CorpseLayer { Name = "Corpses", ZIndex = 2 };
        var troops = new TroopRenderer { Name = "Troops", ZIndex = 5 };
        var chiefs = new ArmyRenderer { Name = "Chiefs", ZIndex = 10 };
        var top = new ObjectiveRenderer { Name = "TopObjectives", ZIndex = 12, Overlay = true };
        var projectiles = new ProjectileRenderer { Name = "Projectiles", ZIndex = 15 };
        var effects = new EffectsLayer { Name = "Effects", ZIndex = 20 };
        var zone = new ZoneRenderer { Name = "Zone", ZIndex = 25 };
        var fog = new FogRenderer { Name = "Fog", ZIndex = 30 };
        var camera = new CameraController { Name = "Camera" };
        var input = new InputMapper { Name = "Input" };
        var audio = new AudioManager { Name = "Audio" };
        var stats = new MatchStats { Name = "Stats" };
        foreach (var node in new Node[] { map, bubbles, ground, corpses, troops, chiefs, top, projectiles, effects, zone, fog, camera, input, audio, stats }) AddChild(node);
        map.Setup(session, camera);
        bubbles.Setup(session, camera);
        ground.Setup(session, camera);
        corpses.Setup(session);
        troops.Setup(session, camera);
        chiefs.Setup(session, camera);
        top.Setup(session, camera);
        projectiles.Setup(session, camera);
        effects.Setup(session, camera);
        zone.Setup(session, camera);
        fog.Setup(session);
        camera.Setup(session, chiefs);
        audio.Setup(session);
        stats.Setup(session);
        UiKit.OnClick = audio.Click;

        // ---- Interface ----
        var ui = new CanvasLayer { Name = "UI" };
        AddChild(ui);
        var weather = new WeatherOverlay { Name = "Weather" };
        var indicators = new ObjectiveIndicators { Name = "Indicators" };
        var eraCard = new EraTransition { Name = "EraTransition" };
        _debug = new DebugHud { Name = "Debug", Visible = false };
        var hud = new GameHud { Name = "Hud" };
        _board = new Leaderboard { Name = "Leaderboard", Visible = false };
        var minimap = new Minimap { Name = "Minimap" };
        _editor = new FormationEditor { Name = "FormationEditor" };
        var end = new EndScreen { Name = "EndScreen" };
        _pause = new PauseMenu { Name = "Pause" };
        foreach (var node in new Node[] { weather, indicators, eraCard, _debug, hud, _board, minimap, _editor, end, _pause }) ui.AddChild(node);
        weather.Setup(session);
        indicators.Setup(session, camera);
        eraCard.Setup(session);
        _debug.Setup(session, camera);
        hud.Setup(session, store, _editor.Open);
        _board.Setup(session);
        minimap.Setup(session, camera, chiefs);
        _editor.Setup(session, store, hud);
        end.Setup(session, stats, Replay, QuitToMenu);
        _pause.Setup(session, QuitToMenu);
        camera.Notify += hud.ShowChip;
        hud.Announced += audio.Speak;
        session.NetworkError += message => hud.Announce(message, stays: true);
        input.Setup(session, camera, chiefs, _editor, store, hud);

        if (session.World.Settings.Mode == GameMode.Tutorial)
        {
            var tutorial = new TutorialController { Name = "Tutorial" };
            ui.AddChild(tutorial);
            tutorial.Setup(session, _profile, _editor, QuitToMenu);
        }

        // On génère tout de suite le terrain autour du chef pour ne pas voir de cases vides au départ.
        map.Preload(session.LocalArmy.General.Position.ToGodot(), 1);
    }

    /// <summary>Retour à l'écran titre (ferme la connexion réseau éventuelle).</summary>
    private void QuitToMenu()
    {
        UiKit.OnClick = null;
        _session.Leave();
        GetTree().ReloadCurrentScene();
    }

    /// <summary>Rejouer une partie solo avec les mêmes réglages (nouvelle carte).</summary>
    private void Replay() => RestartSolo(GameSession.LastSoloSettings ?? new MatchSettings());

    private void RestartSolo(MatchSettings settings)
    {
        UiKit.OnClick = null;
        _autoSolo = settings with { Seed = GD.Randi() };
        _session.Leave();
        GetTree().ReloadCurrentScene();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_session == null || @event is not InputEventKey { Pressed: true, Echo: false } key || !_session.InMatch) return;
        if (key.IsActionPressed("pause") && _pause != null && !(_editor?.IsOpen ?? false))
        {
            if (_pause.IsOpen) _pause.Close(); else _pause.Open();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.IsActionPressed("leaderboard") && _board != null) { _board.Visible = !_board.Visible; return; }
        bool solo = _session.Mode == SessionMode.Solo;
        var last = GameSession.LastSoloSettings ?? new MatchSettings();
        switch (key.Keycode)
        {
            case Key.F2 when solo: RestartSolo(last with { Theme = (MapTheme)(((int)last.Theme + 1) % 4) }); break;
            case Key.F2: QuitToMenu(); break;
            case Key.F3 when _debug != null: _debug.Visible = !_debug.Visible; break;
            case Key.F8 when solo: RestartSolo(last with { Mode = last.Mode == GameMode.Classic ? GameMode.Quick : last.Mode == GameMode.Quick ? GameMode.Sandbox : GameMode.Classic }); break;
            case Key.F9 when solo: RestartSolo(last with { Difficulty = (BotDifficulty)(((int)last.Difficulty + 1) % 3) }); break;
            case Key.F10 when solo: RestartSolo(last with { FogOfWar = !last.FogOfWar }); break;
        }
    }
}
