using ChronoClash.Core.Simulation;
using ChronoClash.Game.Input;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using ChronoClash.Game.Presentation;
using ChronoClash.Game.UI;
using Godot;

namespace ChronoClash.Game.Bootstrap;

/// <summary>
/// Point d'entrée du jeu : assemble la partie et tous les éléments d'affichage.
/// L'ordre d'ajout compte : la session (simulation) d'abord, puis la carte, les bulles, les troupes, les chefs,
/// les effets, la caméra, les contrôles et enfin l'interface par-dessus.
/// (À l'étape M6, l'écran titre choisira les réglages avant de lancer cette scène.)
///
/// Raccourcis : L = classement · F2 = nouvelle carte avec le thème suivant · F3 = panneau technique ·
/// F8 = mode (classique / rapide / bac à sable) · F9 = difficulté des bots · F10 = brouillard de guerre.
/// </summary>
public partial class GameRoot : Node2D
{
    // Réglages de la prochaine partie (statiques pour survivre au rechargement de la scène).
    // À l'étape M6, l'écran titre les remplacera.
    /// <summary>Thème de la carte (F2 passe au suivant avec une nouvelle carte).</summary>
    private static int _themeIndex;
    /// <summary>Mode : classique, rapide, bac à sable (F8).</summary>
    private static GameMode _mode = GameMode.Classic;
    /// <summary>Difficulté des bots (F9).</summary>
    private static BotDifficulty _difficulty = BotDifficulty.Normal;
    /// <summary>Brouillard de guerre (F10).</summary>
    private static bool _fog = true;

    private DebugHud _debug = null!;
    private Leaderboard _board = null!;

    public override void _Ready()
    {
        // AllowCheats : autorise F4 (+20 troupes) pendant le développement.
        var settings = new MatchSettings
        {
            Seed = GD.Randi(), Theme = (MapTheme)_themeIndex, Mode = _mode, Difficulty = _difficulty, FogOfWar = _fog, AllowCheats = true,
        };
        var store = FormationStore.Load();

        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.StartSolo(settings, "Toi", 0xF0B52A);
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
        ground.Setup(session, camera);
        top.Setup(session, camera);
        zone.Setup(session, camera);
        fog.Setup(session);
        map.Setup(session, camera);
        bubbles.Setup(session, camera);
        corpses.Setup(session);
        projectiles.Setup(session, camera);
        troops.Setup(session, camera);
        chiefs.Setup(session, camera);
        effects.Setup(session, camera);
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
        _board.Setup(session);
        _debug.Setup(session, camera);
        hud.Setup(session, store, editor.Open);
        minimap.Setup(session, camera, chiefs);
        editor.Setup(session, store, hud);
        camera.Notify += hud.ShowChip;

        input.Setup(session, camera, chiefs, editor, store, hud);

        // On génère tout de suite le terrain autour du chef pour ne pas voir de cases vides au départ.
        map.Preload(session.LocalArmy.General.Position.ToGodot(), 1);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.F2)
        {
            _themeIndex = (_themeIndex + 1) % 4;
            GetTree().ReloadCurrentScene();
        }
        else if (key.Keycode == Key.F3) _debug.Visible = !_debug.Visible;
        else if (key.Keycode == Key.L) _board.Visible = !_board.Visible;
        else if (key.Keycode == Key.F8) { _mode = _mode == GameMode.Classic ? GameMode.Quick : _mode == GameMode.Quick ? GameMode.Sandbox : GameMode.Classic; GetTree().ReloadCurrentScene(); }
        else if (key.Keycode == Key.F9) { _difficulty = (BotDifficulty)(((int)_difficulty + 1) % 3); GetTree().ReloadCurrentScene(); }
        else if (key.Keycode == Key.F10) { _fog = !_fog; GetTree().ReloadCurrentScene(); }
    }
}
