using ChronoClash.Core;
using ChronoClash.Game.Net;
using ChronoClash.Game.Presentation;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Panneau d'informations techniques en haut à gauche (images par seconde, graine, position…)
/// et rappel des commandes. F3 l'affiche ou le cache (géré par GameRoot).
/// </summary>
public partial class DebugHud : Control
{
    private GameSession _session = null!;
    private CameraController _camera = null!;
    private Label _info = null!;

    public void Setup(GameSession session, CameraController camera)
    {
        _session = session;
        _camera = camera;
        MouseFilter = MouseFilterEnum.Ignore;

        _info = MakeLabel(15);
        _info.Position = new Vector2(16, 12);
        AddChild(_info);

    }

    private static Label MakeLabel(int size)
    {
        var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        label.AddThemeConstantOverride("outline_size", 5);
        return label;
    }

    public override void _Process(double delta)
    {
        var world = _session.World;
        var g = _session.LocalArmy.General;
        _info.Text =
            $"{CoreInfo.GameName} — {CoreInfo.Version}   ({Engine.GetFramesPerSecond():0} images/s)\n" +
            $"Carte : {world.Settings.Theme}   graine {world.Settings.Seed}   temps {world.TimeSeconds:0.0} s\n" +
            $"Chef : ({g.Position.X:0}, {g.Position.Y:0})   vitesse {g.Velocity.Length:0}   endurance {g.Stamina:0} %{(g.Exhausted ? " (épuisé)" : g.Sprinting ? " (sprint)" : "")}\n" +
            $"Zoom {_camera.Zoom.X:0.00}   caméra {(_camera.Locked ? "verrouillée" : "libre")}   bulles {world.Bubbles.Count}   troupes {_session.LocalArmy.Units.Count}\n\n" +
            "ZQSD / flèches : marcher · clic droit : aller (maintenu = suivre) · Maj + clic droit : ajouter une étape · Espace : sprint · X : halte\n" +
            "1 charge · 2 retrait · 3 viser (curseur) · R cri de guerre · 4 / 5 / 6 : acheter · Tab ou 7 8 9 0 : formation · F : éditeur\n" +
            "Tests : F4 +20 troupes · F6 clan ennemi (Maj + F6 : 2 ères d'avance) · F7 +50 ★\n" +
            "Molette : zoom · Y : verrouiller · C : recentrer · clic molette : glisser · L : classement\n" +
            "F2 : nouvelle carte (thème suivant) · F8 : changer de mode · F9 : changer de difficulté · F10 : brouillard on/off · F3 : cacher ce panneau";

    }
}
