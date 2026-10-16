using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using ChronoClash.Core.Systems;
using Xunit;

namespace ChronoClash.Core.Tests;

/// <summary>Tests de l'étape M4 : zone, objectifs, événements, brouillard, bots et fin de partie.</summary>
public class BattleRoyaleTests
{
    private static void Run(Match match, float seconds)
    {
        for (int i = 0; i < seconds * TickClock.TicksPerSecond; i++) match.Step();
    }

    private static Match Solo(GameMode mode = GameMode.Classic, uint seed = 55)
    {
        var match = new Match(new MatchSettings { Seed = seed, Mode = mode, AllowCheats = true });
        match.AddArmy(1, "Testeur", 0xF0B52A, isBot: false);
        return match;
    }

    [Fact]
    public void Zone_StartsShrinkingAfterTheFirstWait_AndHurtsOnlyThoseOutside()
    {
        var match = Solo(GameMode.Quick);
        var world = match.World;
        float wait = world.Data.Tuning.Zone.Phases[0].Wait * world.Data.Tuning.QuickMode.ZoneFactor;
        Run(match, wait + 1);
        Assert.Equal(ZoneStage.Shrinking, world.Zone.Stage);
        Assert.True(world.Zone.DamagePercent > 0);

        // Un chef placé loin hors de la zone perd de la vie ; le nôtre, dedans, non (s'il n'est pas dehors).
        var outsider = world.CreateArmy(2, "Dehors", 0x3366CC, isBot: true, new Vec2(250, 250));
        outsider.IsPassive = true;
        // On fige une petite zone loin de lui (en « attente », la zone ne bouge pas, mais les dégâts restent actifs).
        world.Zone.Stage = ZoneStage.Waiting;
        world.Zone.Timer = 0;
        world.Zone.Center = new Vec2(8000, 8000);
        world.Zone.Radius = 500;
        float before = outsider.General.Hp;
        Run(match, 1.1f);
        Assert.True(outsider.General.Hp < before);
    }

    [Fact]
    public void Crate_GivesResourcesPointsAndTroops()
    {
        var match = Solo();
        match.Step();
        var world = match.World;
        var army = world.Armies[0];
        world.Crate = new Crate(army.General.Position, 0);
        int res = army.Resources, pts = army.Points, units = army.Units.Count;
        match.Step();
        Assert.Null(world.Crate);
        Assert.Equal(res + world.Data.Tuning.Objectives.Crate.Resources, army.Resources);
        Assert.True(army.Points >= pts + world.Data.Tuning.Objectives.Crate.Points);
        Assert.Equal(units + world.Data.Tuning.Objectives.Crate.Troops.Count, army.Units.Count);
    }

    [Fact]
    public void Portal_EvolvesTroopsToTheNextEra()
    {
        var match = Solo();
        match.Enqueue(new DebugSpawnTroopsCommand(0, 1, 12));
        match.Step();
        var world = match.World;
        var army = world.Armies[0];
        world.Portals.Add(new Portal(9999, army.General.Position, 0));
        match.Step();
        Assert.Contains(army.Units, u => u.Era == 1);
        Assert.Contains(match.Events, e => e is PortalUsedEvent);
    }

    [Fact]
    public void Shrine_IsCapturedAfterStayingAlone_AndGivesItsBonus()
    {
        var match = Solo();
        match.Step();
        var world = match.World;
        var army = world.Armies[0];
        world.Shrines.Clear();
        world.Shrines.Add(new Shrine(9999, ShrineKind.Wind, army.General.Position));
        Run(match, world.Data.Tuning.Objectives.Shrines.CaptureTime + 0.5f);
        Assert.Equal(army, world.Shrines[0].Owner);
        Assert.True(army.Bonus.Speed > 1.1f);
    }

    [Fact]
    public void KillingTheBoss_RewardsTheKiller()
    {
        var match = Solo();
        match.Step();
        var world = match.World;
        var army = world.Armies[0];
        world.Boss = new Boss(world.Neutral, army.General.Position + new Vec2(2000, 0), 0, 0, 100, 10, 2);
        int before = army.Points;
        CombatRules.ApplyDamage(world, army, world.Boss, 1e6f, 0);
        match.Step();
        Assert.Null(world.Boss);
        Assert.Equal(before + world.Data.Tuning.Objectives.Boss.Points, army.Points);
    }

    [Fact]
    public void Vision_SeesNearbyButNotFarAway()
    {
        var match = Solo();
        match.Step();
        var world = match.World;
        var me = world.Armies[0];
        var vision = world.Vision[me.Id];
        Assert.True(vision.IsVisible(me.General.Position + new Vec2(300, 0)));
        Assert.False(vision.IsVisible(me.General.Position + new Vec2(3000, 0)));
    }

    [Fact]
    public void Bots_PlayOnTheirOwn()
    {
        var match = Solo(GameMode.Classic, 66);
        match.AddBots();
        var world = match.World;
        Assert.Equal(MatchSettings.MaxClans, world.Armies.Count);
        var starts = world.Armies.Select(a => a.General.Position).ToList();
        Run(match, 90);
        var bots = world.Armies.Where(a => a.IsBot).ToList();
        Assert.All(bots, b => Assert.NotNull(b.Brain));
        // Au bout d'1 min 30, les bots ont bougé et ramassé des bulles.
        Assert.True(bots.Count(b => Vec2.Distance(b.General.Position, starts[b.Id]) > 300) >= 6, "Les bots ne bougent presque pas");
        Assert.True(bots.Average(b => b.Units.Count) >= 5, "Les bots ne lèvent pas d'armée");
    }

    [Fact]
    public void LastClanStanding_WinsTheMatch()
    {
        var match = Solo();
        var world = match.World;
        var rival = world.CreateArmy(2, "Rival", 0x3366CC, isBot: true, world.Armies[0].General.Position + new Vec2(3000, 0));
        match.Step();
        CombatRules.ApplyDamage(world, world.Armies[0], rival.General, 1e6f, 0);
        match.Step();
        Assert.True(world.IsOver);
        Assert.Equal(world.Armies[0], world.Winner);
        Assert.Equal(1, world.Armies[0].Placement);
        Assert.Equal(2, rival.Placement);
    }
}
