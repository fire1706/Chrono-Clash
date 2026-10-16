using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Interface de jeu : panneau des ressources en haut à droite, barre d'actions en bas
/// (achats 4/5/6 et formation), et messages courts au centre en haut.
/// Le joueur peut aussi cliquer sur les boutons de la barre.
/// </summary>
public partial class GameHud : Control
{
    private GameSession _session = null!;
    private FormationStore _store = null!;
    private Action _openEditor = null!;

    private Label _resources = null!;
    private Label _troops = null!;
    private readonly Button[] _buy = new Button[3];
    private Button _formation = null!;
    private HBoxContainer _bar = null!;
    private PanelContainer _panel = null!;
    private Label _chip = null!;
    private float _chipTime;

    public void Setup(GameSession session, FormationStore store, Action openEditor)
    {
        _session = session;
        _store = store;
        _openEditor = openEditor;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // seuls les boutons attrapent la souris
        Build();
        _session.EventRaised += OnEvent;
    }

    public override void _ExitTree() => _session.EventRaised -= OnEvent;

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = new Color(0.1f, 0.08f, 0.06f, 0.85f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, ContentMarginLeft = 12, ContentMarginRight = 12,
        ContentMarginTop = 8, ContentMarginBottom = 8,
    };

    private void Build()
    {
        // ---- Panneau en haut à droite ----
        _panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        _panel.AddThemeStyleboxOverride("panel", PanelStyle());
        var lines = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _panel.AddChild(lines);
        _resources = new Label();
        _resources.AddThemeFontSizeOverride("font_size", 20);
        _troops = new Label();
        lines.AddChild(_resources);
        lines.AddChild(_troops);
        AddChild(_panel);

        // ---- Barre d'actions en bas ----
        _bar = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _bar.AddThemeConstantOverride("separation", 8);
        AddChild(_bar);
        var data = _session.World.Data;
        for (int k = 0; k < 3; k++)
        {
            var kind = (TroopKind)k;
            var button = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(116, 64) };
            button.AddThemeColorOverride("font_color", Color.FromHtml(data.Troop(kind).Color).Lightened(0.35f));
            button.Pressed += () => _session.Send(new BuyCommand(_session.World.Tick, _session.LocalPlayerId, kind));
            _buy[k] = button;
            _bar.AddChild(button);
        }
        _formation = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 64) };
        _formation.Pressed += () => _openEditor();
        _bar.AddChild(_formation);

        // ---- Messages courts ----
        _chip = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _chip.AddThemeFontSizeOverride("font_size", 18);
        _chip.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _chip.AddThemeConstantOverride("outline_size", 5);
        AddChild(_chip);
    }

    /// <summary>Affiche un message court au centre en haut de l'écran.</summary>
    public void ShowChip(string text)
    {
        _chip.Text = text;
        _chipTime = 1.8f;
    }

    private void OnEvent(GameEvent e)
    {
        switch (e)
        {
            case PathFailedEvent f when f.PlayerId == _session.LocalPlayerId:
                ShowChip("Impossible d'aller là");
                break;
            case BuyFailedEvent b when b.PlayerId == _session.LocalPlayerId:
                ShowChip($"Pas assez de ressources : {b.Cost} ◆ nécessaires (tu en as {b.Have})");
                break;
        }
    }

    public override void _Process(double delta)
    {
        var army = _session.LocalArmy;
        var data = _session.World.Data;
        var counts = army.CountByKind();
        _resources.Text = $"◆ {army.Resources}     ★ {army.Points}";
        _troops.Text = $"{counts[0] + counts[1] + counts[2]} troupes : {counts[0]} guerriers · {counts[1]} distance · {counts[2]} tanks\n{data.Eras[army.Era].Name}";

        for (int k = 0; k < 3; k++)
        {
            var kind = (TroopKind)k;
            int cost = data.Troop(kind).Cost;
            _buy[k].Text = $"{k + 4} · {data.Stats(kind, army.Era).Short}\n{cost} ◆";
            _buy[k].Modulate = new Color(1, 1, 1, army.Resources >= cost ? 1f : 0.5f); // grisé si trop cher
        }
        _formation.Text = $"F · Tab\nFormation : {_store.Names[army.FormationIndex]}";

        // Placement (l'écran peut changer de taille).
        var size = GetViewportRect().Size;
        _panel.Position = new Vector2(size.X - _panel.Size.X - 12, 12);
        _bar.Position = new Vector2((size.X - _bar.Size.X) / 2, size.Y - _bar.Size.Y - 14);
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
