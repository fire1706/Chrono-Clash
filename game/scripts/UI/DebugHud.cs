using ChronoClash.Core;
using ChronoClash.Core.Events;
using ChronoClash.Game.Net;
using ChronoClash.Game.Presentation;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Interface provisoire de l'étape M1 : informations techniques en haut à gauche, rappel des commandes,
/// et petits messages au centre (« Caméra libre », « Impossible d'aller là »…).
/// Elle sera remplacée par le vrai HUD à l'étape M6.
/// </summary>
public partial class DebugHud : Control
{
    private GameSession _session = null!;
    private CameraController _camera = null!;
    private Label _info = null!;
    private Label _chip = null!;
    private float _chipTime;

    public void Setup(GameSession session, CameraController camera)
    {
        _session = session;
        _camera = camera;
        MouseFilter = MouseFilterEnum.Ignore;

        _info = MakeLabel(15);
        _info.Position = new Vector2(16, 12);
        AddChild(_info);

        _chip = MakeLabel(18);
        _chip.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_chip);

        _camera.Notify += ShowChip;
        _session.EventRaised += OnEvent;
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

    public void ShowChip(string text)
    {
        _chip.Text = text;
        _chipTime = 1.8f;
    }

    private void OnEvent(GameEvent e)
    {
        if (e is PathFailedEvent failed && failed.PlayerId == _session.LocalPlayerId) ShowChip("Impossible d'aller là");
    }

    public override void _ExitTree()
    {
        // On se désabonne pour ne pas garder de référence vers une scène détruite (rechargement avec F2).
        _camera.Notify -= ShowChip;
        _session.EventRaised -= OnEvent;
    }

    public override void _Process(double delta)
    {
        var world = _session.World;
        var g = _session.LocalArmy.General;
        _info.Text =
            $"{CoreInfo.GameName} — {CoreInfo.Version}   ({Engine.GetFramesPerSecond():0} images/s)\n" +
            $"Carte : {world.Settings.Theme}   graine {world.Settings.Seed}   temps {world.TimeSeconds:0.0} s\n" +
            $"Chef : ({g.Position.X:0}, {g.Position.Y:0})   vitesse {g.Velocity.Length:0}   endurance {g.Stamina:0} %{(g.Exhausted ? " (épuisé)" : g.Sprinting ? " (sprint)" : "")}\n" +
            $"Zoom {_camera.Zoom.X:0.00}   caméra {(_camera.Locked ? "verrouillée" : "libre")}\n\n" +
            "ZQSD / flèches : marcher   ·   clic droit : aller (maintenu = suivre)   ·   Maj + clic droit : ajouter une étape\n" +
            "Espace : sprint   ·   X : halte   ·   molette : zoom   ·   Y : verrouiller   ·   C : recentrer   ·   clic molette : glisser\n" +
            "F2 : nouvelle carte (thème suivant)";

        var size = GetViewportRect().Size;
        _chip.Size = new Vector2(size.X, 30);
        _chip.Position = new Vector2(0, 18);
        if (_chipTime > 0)
        {
            _chipTime -= (float)delta;
            _chip.Modulate = new Color(1, 1, 1, Mathf.Clamp(_chipTime / 0.4f, 0, 1));
        }
        else _chip.Text = "";
    }
}
