using ChronoClash.Core.Simulation;
using ChronoClash.Game.Input;
using ChronoClash.Game.Net;
using ChronoClash.Game.Presentation;
using ChronoClash.Game.UI;
using Godot;

namespace ChronoClash.Game.Bootstrap;

/// <summary>
/// Point d'entrée du jeu : assemble la partie et tous les éléments d'affichage.
/// L'ordre d'ajout compte : la session (simulation) d'abord, puis la carte, les armées, la caméra,
/// les contrôles et enfin l'interface par-dessus.
/// (À l'étape M6, l'écran titre choisira les réglages avant de lancer cette scène.)
/// </summary>
public partial class GameRoot : Node2D
{
    /// <summary>Thème de la prochaine carte (F2 passe au suivant). Statique pour survivre au rechargement de la scène.</summary>
    private static int _themeIndex;

    public override void _Ready()
    {
        var settings = new MatchSettings { Seed = GD.Randi(), Theme = (MapTheme)_themeIndex };

        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.StartSolo(settings, "Toi", 0xF0B52A);

        var map = new MapRenderer { Name = "Map", ZIndex = -10 };
        var armies = new ArmyRenderer { Name = "Armies", ZIndex = 10 };
        var camera = new CameraController { Name = "Camera" };
        var input = new InputMapper { Name = "Input" };
        AddChild(map);
        AddChild(armies);
        AddChild(camera);
        AddChild(input);
        map.Setup(session, camera);
        armies.Setup(session, camera);
        camera.Setup(session, armies);
        input.Setup(session, camera, armies);

        var ui = new CanvasLayer { Name = "UI" };
        AddChild(ui);
        var hud = new DebugHud { Name = "Hud" };
        ui.AddChild(hud);
        hud.Setup(session, camera);
        var minimap = new Minimap { Name = "Minimap" };
        ui.AddChild(minimap);
        minimap.Setup(session, camera, armies);

        // On génère tout de suite le terrain autour du chef pour ne pas voir de cases vides au départ.
        map.Preload(session.LocalArmy.General.Position.ToGodot(), 1);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F2 })
        {
            _themeIndex = (_themeIndex + 1) % 4;
            GetTree().ReloadCurrentScene();
        }
    }
}
