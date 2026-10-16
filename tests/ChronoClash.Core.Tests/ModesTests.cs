using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using ChronoClash.Core.Systems;
using Xunit;

namespace ChronoClash.Core.Tests;

/// <summary>Tests de l'étape M6 : tutoriel, bac à sable, emblèmes transmis en réseau.</summary>
public class ModesTests
{
    [Fact]
    public void Tutorial_HasNoBots_NoZone_AndNeverEndsOnItsOwn()
    {
        var (hostEnd, _) = LocalTransport.CreatePair();
        var host = new HostSession(hostEnd, null, "Élève", 0xF0B52A);
        var match = host.StartMatch(new MatchSettings { Seed = 90, Mode = GameMode.Tutorial, AllowCheats = true });
        Assert.Single(match.World.Armies);
        for (int i = 0; i < 300 * TickClock.TicksPerSecond; i++) match.Step(); // 5 minutes
        Assert.Equal(ZoneStage.Waiting, match.World.Zone.Stage);
        Assert.Null(match.World.Boss);
        // Le clan d'entraînement (passif) ne déclenche pas la fin de partie.
        Recruitment.SpawnTestEnemy(match.World, match.World.Armies[0], 5, 0);
        match.Step();
        Assert.False(match.World.IsOver);
    }

    [Fact]
    public void Sandbox_ZoneNeverCloses()
    {
        var match = new Match(new MatchSettings { Seed = 91, Mode = GameMode.Sandbox });
        match.AddArmy(1, "Testeur", 0xF0B52A, isBot: false);
        match.AddBots();
        for (int i = 0; i < 260 * TickClock.TicksPerSecond; i++) match.Step();
        Assert.Equal(ZoneStage.Waiting, match.World.Zone.Stage);
        Assert.Equal(0, match.World.Zone.DamagePercent);
    }

    [Fact]
    public void Emblems_ReachTheClient()
    {
        var (hostLocal, _) = LocalTransport.CreatePair();
        var (hostNet, clientNet) = LocalTransport.CreatePair();
        var host = new HostSession(hostLocal, hostNet, "Hôte", 0xF0B52A, hostEmblem: 5);
        var client = new RemoteClient(clientNet, "Ami", 0x3366CC, emblem: 3);
        hostNet.SimulateConnect();
        for (int i = 0; i < 5; i++) { host.Update(0.05); client.Update(0.05); }
        host.StartMatch(new MatchSettings { Seed = 92 });
        for (int i = 0; i < 5; i++) { host.Update(0.05); client.Update(0.05); }
        Assert.Equal(5, client.World!.FindArmyOfPlayer(1)!.Emblem);
        Assert.Equal(3, client.World.FindArmyOfPlayer(2)!.Emblem);
    }
}
