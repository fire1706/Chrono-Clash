using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Game.Net;
using Godot;

namespace ChronoClash.Game.Stats;

/// <summary>Un relevé toutes les 5 secondes, pour la courbe de fin de partie.</summary>
public readonly record struct StatSample(double Time, int Troops, int Kills, int Resources, int Points, int Era);

/// <summary>
/// Statistiques de TA partie, relevées par cette machine (elles marchent donc aussi pour un client réseau) :
/// courbe (troupes, victimes, ressources, points, ère), et compteurs (bulles, largages, portails, sanctuaires,
/// boss, tactiques, meilleur multiplicateur…). Utilisées par l'écran de fin et les défis.
/// </summary>
public partial class MatchStats : Node
{
    private const double SampleInterval = 5;

    private GameSession _session = null!;
    private double _sampleTimer;

    public List<StatSample> Timeline { get; } = new();
    public int Bubbles, Stars, Crates, Portals, Shrines, Bosses, Lost, BestMultiplier = 1, PeakTroops;
    /// <summary>Victimes par type (Guerrier, Distance, Tank, Chef).</summary>
    public int[] KillsByKind { get; } = new int[4];
    public HashSet<TacticKind> TacticsUsed { get; } = new();
    /// <summary>Moment de l'élimination de ton clan (secondes), ou -1.</summary>
    public double EliminatedAt = -1;

    public void Setup(GameSession session)
    {
        _session = session;
        _session.EventRaised += OnEvent;
    }

    public override void _ExitTree() => _session.EventRaised -= OnEvent;

    private void OnEvent(GameEvent e)
    {
        var me = _session.LocalArmy.Id;
        switch (e)
        {
            case BubbleCollectedEvent b when b.ArmyId == me: if (b.Kind == Core.Entities.BubbleKind.Star) Stars++; else Bubbles++; break;
            case CrateCollectedEvent c when c.ArmyId == me: Crates++; break;
            case PortalUsedEvent p when p.ArmyId == me: Portals++; break;
            case ShrineCapturedEvent s when s.ArmyId == me: Shrines++; break;
            case BossKilledEvent k when k.KillerArmyId == me: Bosses++; break;
            case TacticUsedEvent t when t.ArmyId == me: TacticsUsed.Add(t.Tactic); break;
            case BodyDiedEvent d when d.KillerArmyId == me:
                KillsByKind[d.IsChief ? 3 : (int)(d.Kind ?? TroopKind.Warrior)]++;
                BestMultiplier = Math.Max(BestMultiplier, d.Multiplier);
                break;
            case BodyDiedEvent d when d.ArmyId == me && !d.IsChief: Lost++; break;
            case ArmyEliminatedEvent el when el.ArmyId == me: EliminatedAt = _session.World.TimeSeconds; Sample(); break;
        }
    }

    public int TotalKills => KillsByKind.Sum();

    public override void _Process(double delta)
    {
        if (!_session.InMatch) return;
        var army = _session.LocalArmy;
        PeakTroops = Math.Max(PeakTroops, army.Units.Count);
        if (!army.Alive || _session.World.IsOver) return;
        _sampleTimer -= delta;
        if (_sampleTimer > 0) return;
        _sampleTimer = SampleInterval;
        Sample();
    }

    private void Sample()
    {
        var a = _session.LocalArmy;
        Timeline.Add(new StatSample(_session.World.TimeSeconds, a.Units.Count, TotalKills, a.Resources, a.Points, a.Era));
    }
}
