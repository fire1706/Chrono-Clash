using ChronoClash.Core.Commands;
using ChronoClash.Core.Formations;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Éditeur de formation (touche F) : on peint les zones des guerriers, des troupes à distance et des tanks
/// autour du chef. Le dessin s'agrandit tout seul avec l'armée en gardant ses proportions.
/// Le jeu continue pendant l'édition. Chaque modification est envoyée à l'hôte et enregistrée.
///
/// Raccourcis dans l'éditeur : 1 guerriers · 2 distance · 3 tanks · 0 gomme · Tab formation suivante ·
/// Entrée utiliser · Échap ou F fermer.
/// </summary>
public partial class FormationEditor : Control
{
    private GameSession _session = null!;
    private FormationStore _store = null!;
    private GameHud _hud = null!;
    private int _editing;

    private readonly Button[] _tabs = new Button[4];
    private readonly Button[] _brushes = new Button[4];
    private FormationGridCanvas _grid = null!;
    private FormationPreviewCanvas _preview = null!;

    public bool IsOpen => Visible;

    public void Setup(GameSession session, FormationStore store, GameHud hud)
    {
        _session = session;
        _store = store;
        _hud = hud;
        Build();
        Visible = false;
    }

    // ================= Construction de l'interface =================

    private void Build()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop; // bloque les clics vers le jeu pendant l'édition

        AddChild(new ColorRect { Color = new Color(0.05f, 0.04f, 0.03f, 0.6f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.1f, 0.08f, 0.97f), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 16, ContentMarginBottom = 16, BorderColor = new Color(0.94f, 0.71f, 0.16f, 0.6f),
            BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
        });
        center.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);

        var title = new Label { Text = "Éditeur de formation" };
        title.AddThemeFontSizeOverride("font_size", 24);
        column.AddChild(title);

        var tabs = new HBoxContainer();
        column.AddChild(tabs);
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            _tabs[i] = MakeButton($"{i + 7 - (i == 3 ? 10 : 0)} · {_store.Names[i]}", () => SelectTab(index));
            _tabs[i].ToggleMode = true;
            tabs.AddChild(_tabs[i]);
        }

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 20);
        column.AddChild(body);
        var gridBox = new VBoxContainer();
        gridBox.AddChild(new Label { Text = "▲ Avant", HorizontalAlignment = HorizontalAlignment.Center });
        _grid = new FormationGridCanvas();
        _grid.Changed += () => _preview.Refresh(_grid.Cells, _session.LocalArmy.CountByKind());
        _grid.EditFinished += SendCurrent;
        gridBox.AddChild(_grid);
        body.AddChild(gridBox);

        var side = new VBoxContainer();
        side.AddThemeConstantOverride("separation", 8);
        body.AddChild(side);
        side.AddChild(new Label { Text = "Pinceau" });
        string[] brushNames = { "0 · Gomme", "1 · Guerriers", "2 · Distance", "3 · Tanks" };
        for (int b = 0; b < 4; b++)
        {
            byte value = (byte)b;
            _brushes[b] = MakeButton(brushNames[b], () => SetBrush(value));
            _brushes[b].ToggleMode = true;
            if (b > 0) _brushes[b].AddThemeColorOverride("font_color", FormationGridCanvas.CellColors[b].Lightened(0.35f));
            side.AddChild(_brushes[b]);
        }
        side.AddChild(new Label { Text = "Aperçu avec ton armée" });
        _preview = new FormationPreviewCanvas();
        side.AddChild(_preview);
        side.AddChild(new Label
        {
            Text = "Clic gauche : peindre · clic droit : effacer\nLe dessin s'agrandit avec l'armée.",
            Modulate = new Color(1, 1, 1, 0.65f),
        });

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        column.AddChild(buttons);
        buttons.AddChild(MakeButton("Effacer", () => { Array.Clear(_grid.Cells); AfterBulkEdit(); }));
        buttons.AddChild(MakeButton("Dessin de départ", () => { _store.ResetToPreset(_editing); _grid.Cells = _store.Cells[_editing]; AfterBulkEdit(); }));
        buttons.AddChild(MakeButton("Utiliser cette formation (Entrée)", UseAndClose));
        buttons.AddChild(MakeButton("Fermer (Échap)", Close));
    }

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
        button.Pressed += onPressed;
        return button;
    }

    // ================= Ouverture et actions =================

    public void Open()
    {
        Visible = true;
        SelectTab(_store.Current);
        SetBrush(_grid.Brush);
    }

    public void Close() => Visible = false;

    private void SelectTab(int index)
    {
        _editing = index;
        for (int i = 0; i < 4; i++) _tabs[i].ButtonPressed = i == index;
        _grid.Cells = _store.Cells[index];
        _grid.QueueRedraw();
        _preview.Refresh(_grid.Cells, _session.LocalArmy.CountByKind());
    }

    private void SetBrush(byte value)
    {
        _grid.Brush = value;
        for (int b = 0; b < 4; b++) _brushes[b].ButtonPressed = b == value;
    }

    private void AfterBulkEdit()
    {
        _grid.QueueRedraw();
        _preview.Refresh(_grid.Cells, _session.LocalArmy.CountByKind());
        SendCurrent();
    }

    /// <summary>Envoie le dessin à l'hôte (qui replace les troupes si c'est la formation utilisée) et l'enregistre.</summary>
    private void SendCurrent()
    {
        _store.Cells[_editing] = _grid.Cells;
        _session.Send(new SetFormationCellsCommand(_session.World.Tick, _session.LocalPlayerId, _editing, (byte[])_grid.Cells.Clone()));
        _store.Save();
    }

    private void UseAndClose()
    {
        _store.Current = _editing;
        _store.Save();
        _session.Send(new SelectFormationCommand(_session.World.Tick, _session.LocalPlayerId, _editing));
        _hud.ShowChip($"Formation : {_store.Names[_editing]}");
        Close();
    }

    /// <summary>Raccourcis clavier de l'éditeur. Tant qu'il est ouvert, les touches ne vont pas au jeu.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.PhysicalKeycode)
        {
            case Key.Key1: SetBrush(1); break;
            case Key.Key2: SetBrush(2); break;
            case Key.Key3: SetBrush(3); break;
            case Key.Key0: SetBrush(0); break;
            case Key.Tab: SelectTab((_editing + (key.ShiftPressed ? 3 : 1)) % 4); break;
            case Key.Enter or Key.KpEnter: UseAndClose(); break;
            case Key.Escape: Close(); break;
            default:
                if (key.Keycode == Key.F) Close();
                else return; // autre touche : on la laisse passer (caméra, etc.)
                break;
        }
        GetViewport().SetInputAsHandled();
    }
}
