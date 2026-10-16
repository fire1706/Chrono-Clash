using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Systems;
using ChronoClash.Game.Localization;
using ChronoClash.Game.Net;
using ChronoClash.Game.Persistence;
using ChronoClash.Game.Settings;
using ChronoClash.Game.UI;
using Godot;

namespace ChronoClash.Game.Tutorial;

/// <summary>
/// Le tutoriel guidé (8 étapes), joué seul sur la carte sans zone ni rivaux :
/// se déplacer, ramasser des bulles, acheter, changer de formation, dessiner sa formation, charger,
/// pousser un cri de guerre, et vaincre un petit clan d'entraînement.
/// Le tutoriel est toujours une partie SOLO : il peut donc agir directement sur le monde de l'hôte
/// (faire apparaître des bulles, offrir des ressources, placer le clan d'entraînement).
/// </summary>
public partial class TutorialController : Control
{
    private GameSession _session = null!;
    private PlayerProfile _profile = null!;
    private FormationEditor _editor = null!;
    private Action _toMenu = null!;

    private int _step = -1;
    private Vec2 _lastPosition;
    private float _moved;
    private int _bubbles, _initialFormation;
    private bool _bought, _editorOpened, _editorDone, _charged, _cried;
    private Army? _dummy;

    private Label _counter = null!, _title = null!, _hint = null!;
    private ProgressBar _bar = null!;

    /// <summary>Les étapes : titre, explication (avec la touche à utiliser), et ce qui la valide.</summary>
    private (string Title, string Hint, Func<bool> Done, Action? Start)[] Steps => new (string, string, Func<bool>, Action?)[]
    {
        ("Déplace ton chef", Loc.F("Utilise {0} {1} {2} {3} (ou les flèches), ou fais un clic droit sur la carte.", Key("move_up"), Key("move_left"), Key("move_down"), Key("move_right")), () => _moved > 350, null),
        ("Ramasse 3 bulles", Loc.T("Passe sur les bulles colorées : chacune te donne une troupe et des ressources ◆."), () => _bubbles >= 3, SpawnBubbles),
        ("Achète une troupe", Loc.F("Appuie sur {0}, {1} ou {2} pour dépenser tes ressources. On t'offre 40 ◆.", Key("buy_warrior"), Key("buy_ranged"), Key("buy_tank")), () => _bought, () => _session.LocalArmy.Resources += 40),
        ("Change de formation", Loc.F("Appuie sur {0}, ou sur {1} {2} {3} {4} pour choisir une formation.", Key("formation_next"), Key("formation_1"), Key("formation_2"), Key("formation_3"), Key("formation_4")), () => _session.LocalArmy.FormationIndex != _initialFormation, () => _initialFormation = _session.LocalArmy.FormationIndex),
        ("Dessine ta formation", Loc.F("Appuie sur {0} pour ouvrir l'éditeur, peins quelques cases, puis ferme-le.", Key("formation_editor")), () => _editorDone, null),
        ("Lance une charge", Loc.F("Appuie sur {0} : 40 % de tes guerriers et tanks foncent devant ton chef.", Key("tactic_charge")), () => _charged, null),
        ("Pousse un cri de guerre", Loc.F("Appuie sur {0} : les troupes proches frappent plus fort pendant 6 s.", Key("tactic_cry")), () => _cried, null),
        ("Élimine le clan d'entraînement", Loc.F("Un clan gris t'attend devant toi. Approche : tes troupes combattent seules. Essaie {0} pour viser.", Key("tactic_aim")), () => _dummy is { Alive: false }, SpawnDummy),
    };

    private static string Key(string action) => KeyBindings.LabelOf(action);

    public void Setup(GameSession session, PlayerProfile profile, FormationEditor editor, Action toMenu)
    {
        _session = session;
        _profile = profile;
        _editor = editor;
        _toMenu = toMenu;
        MouseFilter = MouseFilterEnum.Ignore;
        Position = new Vector2(16, 200);
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelStyle(0.9f));
        AddChild(panel);
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        panel.AddChild(column);
        _counter = UiKit.Text("", 12, false);
        _counter.AddThemeColorOverride("font_color", UiKit.Gold);
        _title = UiKit.Text("", 22, false);
        _hint = UiKit.Text("", 14);
        _hint.CustomMinimumSize = new Vector2(320, 0);
        _bar = new ProgressBar { MinValue = 0, MaxValue = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(300, 8) };
        foreach (var c in new Control[] { _counter, _title, _hint, _bar }) column.AddChild(c);
        column.AddChild(UiKit.Button("Passer le tutoriel", Finish, 200, 30));
        _session.EventRaised += OnEvent;
        _lastPosition = _session.LocalArmy.General.Position;
        Next();
    }

    public override void _ExitTree() => _session.EventRaised -= OnEvent;

    private void OnEvent(GameEvent e)
    {
        var me = _session.LocalArmy.Id;
        switch (e)
        {
            case BubbleCollectedEvent b when b.ArmyId == me: _bubbles++; break;
            case TroopRecruitedEvent r when r.ArmyId == me: _bought = true; break;
            case TacticUsedEvent t when t.ArmyId == me && t.Tactic == TacticKind.Charge: _charged = true; break;
            case TacticUsedEvent t when t.ArmyId == me && t.Tactic == TacticKind.WarCry: _cried = true; break;
        }
    }

    public override void _Process(double delta)
    {
        if (_step < 0 || _step >= Steps.Length) return;
        var g = _session.LocalArmy.General;
        _moved += Vec2.Distance(g.Position, _lastPosition);
        _lastPosition = g.Position;
        if (_editor.IsOpen) _editorOpened = true;
        else if (_editorOpened) _editorDone = true;
        if (Steps[_step].Done()) Next();
    }

    private void Next()
    {
        _step++;
        if (_step >= Steps.Length) { Finish(); return; }
        var s = Steps[_step];
        s.Start?.Invoke();
        _counter.Text = Loc.F("Tutoriel · étape {0} / {1}", _step + 1, Steps.Length);
        _title.Text = Loc.T(s.Title);
        _hint.Text = s.Hint;
        _bar.Value = _step / (double)Steps.Length;
    }

    private void SpawnBubbles()
    {
        var world = _session.Match!.World;
        var g = _session.LocalArmy.General.Position;
        for (int i = 0; i < 8; i++)
        {
            var p = g + Vec2.FromAngle(i * MathF.Tau / 8) * (180 + i * 15);
            if (world.Terrain.IsWalkable(p, 18)) BubbleSystem.Add(world, i % 3 == 2 ? BubbleKind.Ranged : BubbleKind.Warrior, p, mature: true);
        }
    }

    private void SpawnDummy()
    {
        var world = _session.Match!.World;
        var me = _session.LocalArmy;
        Recruitment.SpawnTestEnemy(world, me, 5, 0);
        _dummy = world.Armies[^1];
        _dummy.General.MaxHp = _dummy.General.Hp = 80; // un chef affaibli : c'est un entraînement
        _dummy.General.Damage = 4;
        Recruitment.SpawnTestTroops(world, me, 6);       // des renforts pour toi
    }

    private void Finish()
    {
        _step = Steps.Length;
        _profile.TutorialDone = true;
        _profile.Save();
        foreach (var child in GetChildren()) child.QueueFree();
        var overlay = new Control();
        GetParent().AddChild(overlay);
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.55f), AnchorRight = 1, AnchorBottom = 1 });
        var column = UiKit.CenteredPanel(overlay, 520);
        column.AddChild(UiKit.Title("Tu es prêt !", 38));
        column.AddChild(UiKit.Text(Loc.T("Tu connais les bases : déplacements, bulles, achats, formations et tactiques. Dans une vraie partie, neuf clans rivaux, la zone et les ères t'attendent."), 15));
        column.AddChild(UiKit.Button("Retour au menu", _toMenu, 480));
    }
}
