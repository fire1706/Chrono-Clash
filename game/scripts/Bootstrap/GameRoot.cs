using ChronoClash.Core.Simulation;
using ChronoClash.Game.Input;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using ChronoClash.Game.Presentation;
using ChronoClash.Game.UI;
using Godot;

namespace ChronoClash.Game.Bootstrap;

/// <summary>
/// Point d'entrée du jeu. Au lancement : l'écran d'accueil / salon (<see cref="LobbyScreen"/>).
/// Quand la partie commence (solo, hébergée ou rejointe), on assemble l'affichage du monde.
/// L'ordre d'ajout compte : la session (simulation) d'abord, puis la carte, les bulles, les troupes, les chefs,
/// les effets, la caméra, les contrôles et enfin l'interface par-dessus.
///
/// Raccourcis : L = classement · F2 = nouvelle partie (solo : nouvelle carte, thème suivant ; en réseau : retour au salon) ·
/// F3 = panneau technique · en solo seulement : F8 mode, F9 difficulté, F10 brouillard.
/// </summary>
public partial class GameRoot : Node2D
{
    /// <summary>Relancer directement une partie solo après un rechargement (F2, F8, F9, F10).</summary>
    private static bool _autoSolo;

    private GameSession _session = null!;
    private PlayerProfile _profile = null!;
    private CanvasLayer? _lobbyLayer;
    private DebugHud? _debug;
    private Leaderboard? _board;

    public override void _Ready()
    {
        _profile = PlayerProfile.Load();
        _session = new GameSession { Name = "Session" };
        AddChild(_session);
        _session.MatchStarted += BuildGame;

        if (_autoSolo)
        {
            _autoSolo = false;
            _session.StartSolo(SoloSettings(), _profile.Name, _profile.ColorRgb);
            return;
        }
        _lobbyLayer = new CanvasLayer { Name = "Lobby" };
        AddChild(_lobbyLayer);
        var lobby = new LobbyScreen { Name = "LobbyScreen" };
        _lobbyLayer.AddChild(lobby);
        lobby.Setup(_session, _profile);
    }

    private MatchSettings SoloSettings() => new()
    {
        Seed = GD.Randi(), Mode = (GameMode)_profile.Mode, Difficulty = (BotDifficulty)_profile.Difficulty,
        Theme = (MapTheme)_profile.Theme, FogOfWar = _profile.Fog, AllowCheats = true,
    };

    /// <summary>Assemble l'affichage de la partie (appelé quand la partie commence).</summary>
    private void BuildGame()
    {
        // On attend la fin de l'image : le salon (qui a déclenché le démarrage) est encore en train de s'exécuter.
        Callable.From(BuildGameNow).CallDeferred();
    }

    private void BuildGameNow()
    {
        _lobbyLayer?.QueueFree();
        _lobbyLayer = null;
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
        foreach (var node in new Node[] { map, bubbles, ground, corpses, troops, chiefs, top, projectiles, effects, zone, fog, camera, input }) AddChild(node);
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

        // ---- Interface ----
        var ui = new CanvasLayer { Name = "UI" };
        AddChild(ui);
        var weather = new WeatherOverlay { Name = "Weather" };
        var indicators = new ObjectiveIndicators { Name = "Indicators" };
        _debug = new DebugHud { Name = "Debug" };
        var hud = new GameHud { Name = "Hud" };
        _board = new Leaderboard { Name = "Leaderboard", Visible = false };
        var minimap = new Minimap { Name = "Minimap" };
        var editor = new FormationEditor { Name = "FormationEditor" };
        foreach (var node in new Node[] { weather, indicators, _debug, hud, _board, minimap, editor }) ui.AddChild(node);
        weather.Setup(session);
        indicators.Setup(session, camera);
        _debug.Setup(session, camera);
        hud.Setup(session, store, editor.Open);
        _board.Setup(session);
        minimap.Setup(session, camera, chiefs);
        editor.Setup(session, store, hud);
        camera.Notify += hud.ShowChip;
        session.NetworkError += message => hud.Announce($"{message}\nF2 : retour à l'accueil", stays: true);
        input.Setup(session, camera, chiefs, editor, store, hud);

        // On génère tout de suite le terrain autour du chef pour ne pas voir de cases vides au départ.
        map.Preload(session.LocalArmy.General.Position.ToGodot(), 1);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key || !_session.InMatch) return;
        bool solo = _session.Mode == SessionMode.Solo;
        switch (key.Keycode)
        {
            case Key.F2:
                if (solo) { _profile.Theme = (_profile.Theme + 1) % 4; _profile.Save(); _autoSolo = true; }
                _session.Leave();
                GetTree().ReloadCurrentScene();
                break;
            case Key.F3 when _debug != null: _debug.Visible = !_debug.Visible; break;
            case Key.L when _board != null: _board.Visible = !_board.Visible; break;
            case Key.F8 when solo: _profile.Mode = (_profile.Mode + 1) % 3; Restart(); break;
            case Key.F9 when solo: _profile.Difficulty = (_profile.Difficulty + 1) % 3; Restart(); break;
            case Key.F10 when solo: _profile.Fog = !_profile.Fog; Restart(); break;
        }
    }

    /// <summary>Relance une partie solo avec les réglages modifiés.</summary>
    private void Restart()
    {
        _profile.Save();
        _autoSolo = true;
        GetTree().ReloadCurrentScene();
    }
}
