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
/// Raccourcis de développement : F2 = nouvelle carte avec le thème suivant · F3 = afficher/cacher le panneau technique.
/// </summary>
public partial class GameRoot : Node2D
{
    /// <summary>Thème de la prochaine carte (F2 passe au suivant). Statique pour survivre au rechargement de la scène.</summary>
    private static int _themeIndex;

    private DebugHud _debug = null!;

    public override void _Ready()
    {
        // AllowCheats : autorise F4 (+20 troupes) pendant le développement.
        var settings = new MatchSettings { Seed = GD.Randi(), Theme = (MapTheme)_themeIndex, AllowCheats = true };
        var store = FormationStore.Load();

        var session = new GameSession { Name = "Session" };
        AddChild(session);
        session.StartSolo(settings, "Toi", 0xF0B52A);
        session.SendFormations(store);

        // ---- Monde (du plus bas au plus haut à l'écran) ----
        var map = new MapRenderer { Name = "Map", ZIndex = -10 };
        var bubbles = new BubbleRenderer { Name = "Bubbles", ZIndex = 0 };
        var troops = new TroopRenderer { Name = "Troops", ZIndex = 5 };
        var chiefs = new ArmyRenderer { Name = "Chiefs", ZIndex = 10 };
        var effects = new EffectsLayer { Name = "Effects", ZIndex = 20 };
        var camera = new CameraController { Name = "Camera" };
        var input = new InputMapper { Name = "Input" };
        foreach (var node in new Node[] { map, bubbles, troops, chiefs, effects, camera, input }) AddChild(node);
        map.Setup(session, camera);
        bubbles.Setup(session, camera);
        troops.Setup(session, camera);
        chiefs.Setup(session, camera);
        effects.Setup(session, camera);
        camera.Setup(session, chiefs);

        // ---- Interface ----
        var ui = new CanvasLayer { Name = "UI" };
        AddChild(ui);
        _debug = new DebugHud { Name = "Debug" };
        var hud = new GameHud { Name = "Hud" };
        var minimap = new Minimap { Name = "Minimap" };
        var editor = new FormationEditor { Name = "FormationEditor" };
        foreach (var node in new Node[] { _debug, hud, minimap, editor }) ui.AddChild(node);
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
    }
}
