using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using Xunit;

namespace ChronoClash.Core.Tests;

/// <summary>Tests de l'étape M7 : serveur dédié, et une partie rapide complète entre bots.</summary>
public class ServerTests
{
    private static void Pump(HostSession host, IEnumerable<RemoteClient> clients, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.05f)
        {
            host.Update(0.05);
            foreach (var c in clients) c.Update(0.05);
        }
    }

    [Fact]
    public void DedicatedServer_FirstPlayerLeadsTheLobby_AndCanStart()
    {
        var (serverNet, clientNet) = LocalTransport.CreatePair();
        var host = HostSession.Dedicated(serverNet);
        var client = new RemoteClient(clientNet, "Chef", 0xF0B52A);
        serverNet.SimulateConnect();
        Pump(host, new[] { client }, 0.2f);
        Assert.True(client.IsLeader);
        Assert.Single(host.Players); // personne ne joue sur le serveur lui-même
        client.RequestMatch(new MatchSettings { Mode = GameMode.Quick, AllowCheats = true }, start: true);
        Pump(host, new[] { client }, 0.3f);
        Assert.NotNull(host.Match);
        Assert.False(host.Match!.Settings.AllowCheats); // les triches restent interdites sur un serveur
        Assert.Equal(MatchSettings.MaxClans, client.World!.Armies.Count);
    }

    [Fact]
    public void DedicatedServer_ReturnsToTheLobby_WhenEveryoneLeaves()
    {
        var (serverNet, clientNet) = LocalTransport.CreatePair();
        var host = HostSession.Dedicated(serverNet);
        var client = new RemoteClient(clientNet, "Chef", 0xF0B52A);
        serverNet.SimulateConnect();
        Pump(host, new[] { client }, 0.2f);
        client.RequestMatch(new MatchSettings(), start: true);
        Pump(host, new[] { client }, 0.2f);
        serverNet.SimulateDisconnect();
        Pump(host, new[] { client }, 0.1f);
        Assert.Null(host.Match);
        Assert.Empty(host.Players);
    }

    [Fact]
    [Trait("Category", "Long")]
    public void QuickMatchBetweenBots_EndsWithAWinner()
    {
        // Partie rapide complète entre 10 bots (environ 30 s de calcul) : vérifie que la zone et les combats
        // finissent bien par désigner un vainqueur.
        var match = new Match(new MatchSettings { Seed = 2024, Mode = GameMode.Quick });
        match.AddBots();
        long maxTicks = 20L * 60 * TickClock.TicksPerSecond;
        while (!match.World.IsOver && match.World.Tick < maxTicks) match.Step();
        Assert.True(match.World.IsOver, $"Pas de vainqueur après {match.World.TimeSeconds / 60:0} min");
        Assert.NotNull(match.World.Winner);
    }
}
