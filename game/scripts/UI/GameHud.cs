using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Game.Localization;
using ChronoClash.Game.Net;
using ChronoClash.Game.Settings;
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
    private Label _zoneLabel = null!;
    private readonly Button[] _tactics = new Button[4];
    private static readonly string[] TacticNames = { "Charge", "Retrait", "Viser", "Cri de guerre" };
    private static readonly string[] TacticActions = { "tactic_charge", "tactic_retreat", "tactic_aim", "tactic_cry" };

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
        _zoneLabel = new Label();
        _zoneLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.7f, 1f));
        lines.AddChild(_zoneLabel);
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

    /// <summary>Viser depuis le bouton : 300 px devant le chef (au clavier, la touche « Viser » vise le curseur).</summary>
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
        Announced?.Invoke(text);
    }

    /// <summary>Chaque grande annonce est aussi lue par l'annonceur (AudioManager s'y abonne).</summary>
    public event Action<string>? Announced;

    /// <summary>Affiche un message court au centre en haut de l'écran (texte déjà traduit).</summary>
    public void ShowChip(string text)
    {
        _chip.Text = text;
        _chipTime = 1.8f;
    }

    /// <summary>Nom d'un clan tel qu'affiché (« Toi » pour le sien), traduit.</summary>
    private string ClanName(int armyId)
    {
        var a = _session.World.Armies[armyId];
        return a == _session.LocalArmy ? Loc.T("Toi") : Loc.T(a.Name);
    }

    private void OnEvent(GameEvent e)
    {
        var data = _session.World.Data;
        var me = _session.LocalArmy;
        switch (e)
        {
            case PathFailedEvent f when f.PlayerId == _session.LocalPlayerId:
                ShowChip(Loc.T("Impossible d'aller là"));
                break;
            case BuyFailedEvent b when b.PlayerId == _session.LocalPlayerId:
                ShowChip(Loc.F("Pas assez de ressources : {0} ◆ nécessaires (tu en as {1})", b.Cost, b.Have));
                break;
            case TacticFailedEvent f when f.PlayerId == _session.LocalPlayerId:
                ShowChip($"{Loc.T(TacticNames[(int)f.Tactic])} : " + (f.Seconds > 0 ? Loc.F("encore {0} s de recharge", (int)MathF.Ceiling(f.Seconds)) : Loc.T(f.Reason)));
                break;
            case TacticUsedEvent t when t.ArmyId == me.Id:
                ShowChip(t.Tactic switch
                {
                    TacticKind.Charge => Loc.F("Charge ! {0} troupes foncent", t.Count),
                    TacticKind.Retreat => Loc.T("Retrait !"),
                    TacticKind.Aim => Loc.T("Toute l'armée vise ce point"),
                    _ => Loc.F("Cri de guerre ! {0} troupes galvanisées", t.Count),
                });
                break;
            case ChiefPromotedEvent p when p.ArmyId == me.Id:
                ShowChip(Loc.F("Ton chef est tombé : un {0} reprend le commandement !", Loc.T(data.Stats(p.HeirKind, me.General.Era).Short).ToLower()));
                break;
            case EraStartedEvent es when es.ArmyId == me.Id:
                Announce(Loc.F("Ton peuple entre dans {0} !", Loc.T(data.Eras[es.Era].To)));
                break;
            case EraCompletedEvent ec when ec.ArmyId == me.Id:
                ShowChip(Loc.F("Évolution terminée : {0}", Loc.T(data.Eras[ec.Era].Name)));
                break;
            case ArmyEliminatedEvent el when el.ArmyId == me.Id:
                Announce(Loc.F("Ton clan a été éliminé — {0}e place", me.Placement));
                break;
            case ArmyEliminatedEvent el when el.KillerArmyId == me.Id:
                Announce(Loc.F("{0} éliminé !", ClanName(el.ArmyId)));
                break;
            case ArmyEliminatedEvent el when me.Alive && el.ClansLeft == 3:
                Announce(Loc.T("Plus que trois clans !"));
                break;
            case ArmyEliminatedEvent el when me.Alive && el.ClansLeft == 2:
                Announce(Loc.T("Duel final !"));
                break;
            case MatchEndedEvent end when end.WinnerArmyId == me.Id:
                Announce(Loc.T("Victoire !"), stays: true);
                break;
            case MatchEndedEvent end when !me.Alive && end.WinnerArmyId >= 0:
                ShowChip(Loc.F("{0} remporte la partie", ClanName(end.WinnerArmyId)));
                break;
            case ZoneShrinkingEvent:
                Announce(Loc.T("La zone se resserre !"));
                break;
            case BossSpawnedEvent boss:
                Announce(Loc.T("Un boss est apparu !") + "\n" + Loc.T(boss.Name));
                break;
            case BossKilledEvent bk when bk.KillerArmyId == me.Id:
                Announce(Loc.T("Géant terrassé !") + $"\n+{bk.Points} ★  +{bk.Resources} ◆");
                break;
            case BossKilledEvent bk when bk.KillerArmyId >= 0:
                ShowChip(Loc.F("{0} a terrassé {1}", ClanName(bk.KillerArmyId), Loc.T(bk.Name)));
                break;
            case CrateAnnouncedEvent:
                ShowChip(Loc.T("Largage temporel dans 20 s : regarde la carte"));
                break;
            case CrateCollectedEvent cc when cc.ArmyId == me.Id:
                ShowChip(Loc.T("Largage récupéré : +60 ◆  +40 ★  +4 troupes"));
                break;
            case PortalUsedEvent pu when pu.ArmyId == me.Id:
                ShowChip(Loc.F("Portail temporel : {0} troupes passent à l'ère suivante", pu.Count));
                break;
            case ShrineCapturedEvent sc when sc.ArmyId == me.Id:
                ShowChip(Loc.T(sc.Kind switch
                {
                    Core.Entities.ShrineKind.Wind => "Sanctuaire du vent capturé : +15 % de vitesse",
                    Core.Entities.ShrineKind.Life => "Sanctuaire de vie capturé : tes troupes se soignent",
                    _ => "Sanctuaire de puissance capturé : +12 % de dégâts",
                }));
                break;
            case WorldEventStartedEvent we:
                switch (we.Kind)
                {
                    case "meteors": Announce(Loc.T("Pluie de météores !")); break;
                    case "storm": Announce(Loc.T("Orage : toutes les armées ralentissent")); break;
                    case "stormEnd": ShowChip(Loc.T("L'orage se calme")); break;
                    case "migration": Announce(Loc.T("Migration de bulles !")); break;
                }
                break;
        }
    }

    public override void _Process(double delta)
    {
        var army = _session.LocalArmy;
        var data = _session.World.Data;
        var counts = army.CountByKind();
        _resources.Text = $"◆ {army.Resources}     ★ {army.Points}";
        string chief = army.General.Dead ? Loc.T("aucun") : army.General.Promoted ? Loc.T("soldat promu") : Loc.T(data.Chiefs[army.General.Era].Name);
        _troops.Text = Loc.F("{0} troupes : {1} guerriers · {2} distance · {3} tanks", counts[0] + counts[1] + counts[2], counts[0], counts[1], counts[2])
                       + "\n" + Loc.F("{0} — chef : {1}", Loc.T(data.Eras[army.Era].Name), chief);

        string[] buyActions = { "buy_warrior", "buy_ranged", "buy_tank" };
        for (int k = 0; k < 3; k++)
        {
            var kind = (TroopKind)k;
            int cost = data.Troop(kind).Cost;
            _buy[k].Text = $"{KeyBindings.LabelOf(buyActions[k])} · {Loc.T(data.Stats(kind, army.Era).Short)}\n{cost} ◆";
            _buy[k].Modulate = new Color(1, 1, 1, army.Resources >= cost ? 1f : 0.5f); // grisé si trop cher
        }
        _formation.Text = $"{KeyBindings.LabelOf("formation_editor")} · {KeyBindings.LabelOf("formation_next")}\n" + Loc.F("Formation : {0}", Loc.T(_store.Names[army.FormationIndex]));

        // Tactiques : nom, touche, et secondes de recharge restantes (bouton grisé pendant la recharge).
        for (int t = 0; t < 4; t++)
        {
            float cd = army.TacticCooldowns[t];
            string head = $"{KeyBindings.LabelOf(TacticActions[t])} · {Loc.T(TacticNames[t])}";
            _tactics[t].Text = cd > 0 ? $"{head}\n{cd:0} s" : $"{head}\n{Loc.T("prêt")}";
            _tactics[t].Modulate = new Color(1, 1, 1, cd > 0 ? 0.5f : 1f);
        }

        // Ère : progression vers la suivante, ou avancement de l'évolution en cours.
        if (army.Evolution is { } evo)
        {
            float q = evo.Progress(data.Tuning.Evolution.Duration);
            _eraLabel.Text = Loc.F("Évolution vers {0} : {1} %", Loc.T(data.Eras[evo.TargetEra].Short), (int)(q * 100));
            _eraBar.Value = q;
        }
        else if (army.Era < Core.Entities.Army.MaxEra)
        {
            int from = _session.World.EraThreshold(army.Era), to = _session.World.EraThreshold(army.Era + 1);
            _eraLabel.Text = Loc.F("{0} dans {1} ★   ·   {2} ennemis abattus", Loc.T(data.Eras[army.Era + 1].Short), Math.Max(0, to - army.Points), army.Kills);
            _eraBar.Value = Math.Clamp((army.Points - from) / (double)(to - from), 0, 1);
        }
        else
        {
            _eraLabel.Text = Loc.F("Dernière ère atteinte   ·   {0} ennemis abattus", army.Kills);
            _eraBar.Value = 1;
        }

        // Zone et clans en vie.
        var world = _session.World;
        float left = Core.Systems.ZoneSystem.TimeLeft(world);
        string time = $"{(int)left / 60}:{(int)left % 60:00}";
        string zone = world.Zone.Stage == Core.Simulation.ZoneStage.Final ? Loc.T("Zone finale")
            : left < 0 ? Loc.T("Pas de zone")
            : world.Zone.Stage == Core.Simulation.ZoneStage.Waiting ? Loc.F("Zone : se resserre dans {0}", time)
            : Loc.F("Zone : se resserre… {0}", time);
        if (world.Zone.IsOutside(army.General.Position) && world.Zone.DamagePercent > 0 && !army.General.Dead) zone += "   " + Loc.T("— tu es HORS de la zone !");
        _zoneLabel.Text = zone + "\n" + Loc.F("{0} clans en vie   ·   {1} · {2}", world.Armies.Count(a => a.Alive && !a.IsPassive), UiKit.ModeName(world.Settings.Mode), UiKit.DifficultyName(world.Settings.Difficulty));

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
