using ChronoClash.Game.Localization;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Menu de pause (touche Échap) : reprendre, réglages, retour au menu principal.
/// En solo, le temps s'arrête ; en multijoueur, la partie continue derrière le menu.
/// </summary>
public partial class PauseMenu : Control
{
    private GameSession _session = null!;
    private Action _quitToMenu = null!;

    public bool IsOpen => Visible;

    public void Setup(GameSession session, Action quitToMenu)
    {
        _session = session;
        _quitToMenu = quitToMenu;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Build();
        Loc.LanguageChanged += Build;
    }

    public override void _ExitTree() => Loc.LanguageChanged -= Build;

    private void Build()
    {
        foreach (var child in GetChildren()) child.QueueFree();
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.5f), AnchorRight = 1, AnchorBottom = 1 });
        var column = UiKit.CenteredPanel(this, 360);
        column.AddChild(UiKit.Title("Pause"));
        if (_session.Mode != SessionMode.Solo) column.AddChild(UiKit.Text(Loc.T("En multijoueur, la partie continue pendant la pause."), 13));
        column.AddChild(UiKit.Button("Reprendre", Close, 320));
        column.AddChild(UiKit.Button("Réglages", () => AddChild(new SettingsPanel(() => { })), 320));
        column.AddChild(UiKit.Button("Quitter la partie", () => { Close(); _quitToMenu(); }, 320));
    }

    public void Open()
    {
        Visible = true;
        _session.Paused = true;
    }

    public void Close()
    {
        Visible = false;
        _session.Paused = false;
    }
}
