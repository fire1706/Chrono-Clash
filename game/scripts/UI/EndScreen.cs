using ChronoClash.Core.Events;
using ChronoClash.Core.Simulation;
using ChronoClash.Game.Localization;
using ChronoClash.Game.Net;
using ChronoClash.Game.Stats;
using Godot;

namespace ChronoClash.Game.UI;

/// <summary>
/// Écran de fin : victoire ou place obtenue, courbe de la partie, chiffres clés, défis débloqués et records battus.
/// Il s'ouvre quand ton clan est éliminé (tu peux alors continuer à regarder) ou quand la partie se termine.
/// </summary>
public partial class EndScreen : Control
{
    private GameSession _session = null!;
    private MatchStats _stats = null!;
    private Action _replay = null!, _menu = null!;
    private bool _recorded;
    private (List<Challenge> Challenges, List<string> Records) _results = (new(), new());

    public void Setup(GameSession session, MatchStats stats, Action replay, Action menu)
    {
        _session = session;
        _stats = stats;
        _replay = replay;
        _menu = menu;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _session.EventRaised += OnEvent;
    }

    public override void _ExitTree() => _session.EventRaised -= OnEvent;

    private void OnEvent(GameEvent e)
    {
        if (_session.World.Settings.Mode == GameMode.Tutorial) return;
        var me = _session.LocalArmy;
        if (e is ArmyEliminatedEvent el && el.ArmyId == me.Id) Open(over: false);
        else if (e is MatchEndedEvent) Open(over: true);
    }

    /// <summary>Enregistre la partie une seule fois (défis et records), puis affiche l'écran.</summary>
    private void Open(bool over)
    {
        var me = _session.LocalArmy;
        if (!_recorded && _session.World.Settings.Mode != GameMode.Tutorial)
        {
            _recorded = true;
            double survival = _stats.EliminatedAt >= 0 ? _stats.EliminatedAt : _session.World.TimeSeconds;
            var summary = new MatchSummary(_session.World.Settings.Mode, _session.World.Winner == me, me.Placement > 0 ? me.Placement : 1,
                me.Era, _stats.PeakTroops, _stats.TotalKills, _stats.BestMultiplier, survival, _stats.Crates, _stats.Portals,
                _stats.Shrines, _stats.Bosses, _stats.TacticsUsed.Count);
            _results = Achievements.Load().Record(summary);
        }
        Build(over);
        Visible = true;
    }

    private void Build(bool over)
    {
        foreach (var child in GetChildren()) child.QueueFree();
        var world = _session.World;
        var me = _session.LocalArmy;
        bool won = world.Winner == me;
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.6f), AnchorRight = 1, AnchorBottom = 1 });
        var column = UiKit.CenteredPanel(this, 760);
        column.AddChild(UiKit.Title(won ? "Victoire !" : "Ton clan est éliminé", 40));
        int minutes = (int)(world.TimeSeconds / 60), seconds = (int)(world.TimeSeconds % 60);
        string place = won ? Loc.T("Dernier clan debout") : Loc.F("{0}e place sur {1}", Math.Max(1, me.Placement), world.Armies.Count(a => !a.IsPassive));
        var sub = UiKit.Text($"{place}   ·   {UiKit.ModeName(world.Settings.Mode)}   ·   {minutes}:{seconds:00}", 16, false);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(sub);

        column.AddChild(new StatsChart { Samples = _stats.Timeline });

        var data = world.Data;
        var k = _stats.KillsByKind;
        column.AddChild(UiKit.Text(
            Loc.F("Ennemis abattus : {0} (guerriers {1}, distance {2}, tanks {3}, chefs {4})   ·   Troupes perdues : {5}", _stats.TotalKills, k[0], k[1], k[2], k[3], _stats.Lost) + "\n" +
            Loc.F("Plus grande armée : {0}   ·   Ère atteinte : {1}   ·   Meilleur multiplicateur : ×{2}", _stats.PeakTroops, Loc.T(data.Eras[me.Era].Name), _stats.BestMultiplier) + "\n" +
            Loc.F("Bulles : {0}   ·   Étoiles : {1}   ·   Largages : {2}   ·   Portails : {3}   ·   Sanctuaires : {4}   ·   Boss : {5}", _stats.Bubbles, _stats.Stars, _stats.Crates, _stats.Portals, _stats.Shrines, _stats.Bosses), 14));

        if (_results.Challenges.Count > 0)
        {
            var l = UiKit.Text(Loc.T("Défis débloqués : ") + string.Join(", ", _results.Challenges.Select(c => "★ " + Loc.T(c.Name))), 15);
            l.AddThemeColorOverride("font_color", UiKit.Gold);
            column.AddChild(l);
        }
        if (_results.Records.Count > 0) column.AddChild(UiKit.Text(Loc.T("Records battus : ") + string.Join(", ", _results.Records), 14));

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        column.AddChild(buttons);
        if (_session.Mode == SessionMode.Solo) buttons.AddChild(UiKit.Button("Rejouer", _replay, 220));
        if (!over) buttons.AddChild(UiKit.Button("Regarder la suite", () => Visible = false, 220));
        buttons.AddChild(UiKit.Button("Menu principal", _menu, 220));
    }
}
