using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Interface de jeu : panneau des ressources et de l'ère en haut à droite, barre d'actions en bas
/// (tactiques 1/2/3/R, achats 4/5/6, formation), messages courts et grandes annonces au centre.
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
    private Label _announce = null!;
    private float _announceTime;
    private bool _announceStays;
    private ProgressBar _eraBar = null!;
    private Label _eraLabel = null!;
    private readonly Button[] _tactics = new Button[4];
    private static readonly string[] TacticNames = { "Charge", "Retrait", "Viser", "Cri de guerre" };
    private static readonly string[] TacticKeys = { "1", "2", "3", "R" };

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
        _eraLabel = new Label();
        lines.AddChild(_eraLabel);
        _eraBar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false, CustomMinimumSize = new Vector2(260, 10), MouseFilter = MouseFilterEnum.Ignore };
        lines.AddChild(_eraBar);
        AddChild(_panel);

        // ---- Barre d'actions en bas ----
        _bar = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _bar.AddThemeConstantOverride("separation", 8);
        AddChild(_bar);
        // Tactiques (touches 1, 2, 3, R).
        for (int t = 0; t < 4; t++)
        {
            var kind = (TacticKind)t;
            var button = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(104, 64) };
            button.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.6f));
            button.Pressed += () => UseTacticFromButton(kind);
            _tactics[t] = button;
            _bar.AddChild(button);
        }
        _bar.AddChild(new VSeparator { CustomMinimumSize = new Vector2(10, 0) });

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
        _bar.AddChild(new VSeparator { CustomMinimumSize = new Vector2(10, 0) });
        _formation = new Button { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(150, 64) };
        _formation.Pressed += () => _openEditor();
        _bar.AddChild(_formation);

        // ---- Messages courts ----
        _chip = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _chip.AddThemeFontSizeOverride("font_size", 18);
        _chip.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        _chip.AddThemeConstantOverride("outline_size", 5);
        AddChild(_chip);

        // ---- Grandes annonces (changement d'ère, élimination…) ----
        _announce = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        _announce.AddThemeFontSizeOverride("font_size", 36);
        _announce.AddThemeColorOverride("font_color", new Color(1f, 0.88f, 0.5f));
        _announce.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _announce.AddThemeConstantOverride("outline_size", 8);
        AddChild(_announce);
    }

    /// <summary>Viser depuis le bouton : 300 px devant le chef (au clavier, la touche 3 vise le curseur).</summary>
    private void UseTacticFromButton(TacticKind kind)
    {
        var g = _session.LocalArmy.General;
        var aim = g.Position + Core.Mathematics.Vec2.FromAngle(_session.LocalArmy.FormationAngle) * 300;
        _session.Send(new TacticCommand(_session.World.Tick, _session.LocalPlayerId, kind, aim.X, aim.Y));
    }

    /// <summary>Grande annonce au centre de l'écran (3 secondes, ou jusqu'à la fin si <paramref name="stays"/>).</summary>
    public void Announce(string text, bool stays = false)
    {
        _announce.Text = text;
        _announceTime = 3f;
        _announceStays = stays;
    }

    /// <summary>Affiche un message court au centre en haut de l'écran.</summary>
    public void ShowChip(string text)
    {
        _chip.Text = text;
        _chipTime = 1.8f;
    }

    private void OnEvent(GameEvent e)
    {
        var data = _session.World.Data;
        switch (e)
        {
            case PathFailedEvent f when f.PlayerId == _session.LocalPlayerId:
                ShowChip("Impossible d'aller là");
                break;
            case BuyFailedEvent b when b.PlayerId == _session.LocalPlayerId:
                ShowChip($"Pas assez de ressources : {b.Cost} ◆ nécessaires (tu en as {b.Have})");
                break;
            case TacticFailedEvent f when f.PlayerId == _session.LocalPlayerId:
                ShowChip($"{TacticNames[(int)f.Tactic]} : {f.Reason}");
                break;
            case TacticUsedEvent t when t.ArmyId == _session.LocalArmy.Id:
                ShowChip(t.Tactic switch
                {
                    TacticKind.Charge => $"Charge ! {t.Count} troupes foncent",
                    TacticKind.Retreat => "Retrait !",
                    TacticKind.Aim => "Toute l'armée vise ce point",
                    _ => $"Cri de guerre ! {t.Count} troupes galvanisées",
                });
                break;
            case ChiefPromotedEvent p when p.ArmyId == _session.LocalArmy.Id:
                ShowChip($"Ton chef est tombé : un {data.Stats(p.HeirKind, _session.LocalArmy.General.Era).Short.ToLower()} reprend le commandement !");
                break;
            case EraStartedEvent es when es.ArmyId == _session.LocalArmy.Id:
                Announce($"Ton peuple entre dans {data.Eras[es.Era].To} !\n{data.Eras[es.Era].Years}");
                break;
            case EraCompletedEvent ec when ec.ArmyId == _session.LocalArmy.Id:
                ShowChip($"Évolution terminée : {data.Eras[ec.Era].Name}");
                break;
            case ArmyEliminatedEvent el when el.ArmyId == _session.LocalArmy.Id:
                Announce("Ton clan a été éliminé\nF2 : nouvelle partie", stays: true);
                break;
            case ArmyEliminatedEvent el when el.KillerArmyId == _session.LocalArmy.Id:
                Announce($"{_session.World.Armies[el.ArmyId].Name} éliminé !");
                break;
        }
    }

    public override void _Process(double delta)
    {
        var army = _session.LocalArmy;
        var data = _session.World.Data;
        var counts = army.CountByKind();
        _resources.Text = $"◆ {army.Resources}     ★ {army.Points}";
        _troops.Text = $"{counts[0] + counts[1] + counts[2]} troupes : {counts[0]} guerriers · {counts[1]} distance · {counts[2]} tanks\n{data.Eras[army.Era].Name} — chef : {(army.General.Dead ? "aucun" : army.General.Promoted ? "soldat promu" : data.Chiefs[army.General.Era].Name)}";

        for (int k = 0; k < 3; k++)
        {
            var kind = (TroopKind)k;
            int cost = data.Troop(kind).Cost;
            _buy[k].Text = $"{k + 4} · {data.Stats(kind, army.Era).Short}\n{cost} ◆";
            _buy[k].Modulate = new Color(1, 1, 1, army.Resources >= cost ? 1f : 0.5f); // grisé si trop cher
        }
        _formation.Text = $"F · Tab\nFormation : {_store.Names[army.FormationIndex]}";

        // Tactiques : nom, touche, et secondes de recharge restantes (bouton grisé pendant la recharge).
        for (int t = 0; t < 4; t++)
        {
            float cd = army.TacticCooldowns[t];
            _tactics[t].Text = cd > 0 ? $"{TacticKeys[t]} · {TacticNames[t]}\n{cd:0} s" : $"{TacticKeys[t]} · {TacticNames[t]}\nprêt";
            _tactics[t].Modulate = new Color(1, 1, 1, cd > 0 ? 0.5f : 1f);
        }

        // Ère : progression vers la suivante, ou avancement de l'évolution en cours.
        if (army.Evolution is { } evo)
        {
            float q = evo.Progress(data.Tuning.Evolution.Duration);
            _eraLabel.Text = $"Évolution vers {data.Eras[evo.TargetEra].Short} : {q * 100:0} %";
            _eraBar.Value = q;
        }
        else if (army.Era < Core.Entities.Army.MaxEra)
        {
            int from = data.Eras[army.Era].Threshold, to = data.Eras[army.Era + 1].Threshold;
            _eraLabel.Text = $"{data.Eras[army.Era + 1].Short} dans {Math.Max(0, to - army.Points)} ★   ·   {army.Kills} ennemis abattus";
            _eraBar.Value = Math.Clamp((army.Points - from) / (double)(to - from), 0, 1);
        }
        else
        {
            _eraLabel.Text = $"Dernière ère atteinte   ·   {army.Kills} ennemis abattus";
            _eraBar.Value = 1;
        }

        // Placement (l'écran peut changer de taille).
        var size = GetViewportRect().Size;
        _panel.Position = new Vector2(size.X - _panel.Size.X - 12, 12);
        _bar.Position = new Vector2((size.X - _bar.Size.X) / 2, size.Y - _bar.Size.Y - 14);
        _chip.Size = new Vector2(size.X, 30);
        _chip.Position = new Vector2(0, 18);
        _announce.Size = new Vector2(size.X, 100);
        _announce.Position = new Vector2(0, size.Y * 0.28f);
        if (_announceStays) _announce.Modulate = Colors.White;
        else if (_announceTime > 0)
        {
            _announceTime -= (float)delta;
            _announce.Modulate = new Color(1, 1, 1, Mathf.Clamp(_announceTime / 0.6f, 0, 1));
        }
        else _announce.Text = "";

        if (_chipTime > 0)
        {
            _chipTime -= (float)delta;
            _chip.Modulate = new Color(1, 1, 1, Mathf.Clamp(_chipTime / 0.4f, 0, 1));
        }
        else _chip.Text = "";
    }
}
