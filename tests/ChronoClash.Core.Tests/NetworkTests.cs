using ChronoClash.Core.Commands;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using Xunit;

namespace ChronoClash.Core.Tests;

/// <summary>
/// Tests de l'étape M5 : le multijoueur, sans vrai réseau. Une paire LocalTransport joue le rôle d'internet
/// entre un hôte et un client : on vérifie le salon, le début de partie, les snapshots, les bulles et les ordres.
/// </summary>
public class NetworkTests
{
    /// <summary>Un hôte (avec son joueur local) et un client distant reliés « par le réseau ».</summary>
    private static (HostSession Host, RemoteClient Client) Connect()
    {
        var (hostLocal, _) = LocalTransport.CreatePair();
        var (hostNet, clientNet) = LocalTransport.CreatePair();
        var host = new HostSession(hostLocal, hostNet, "Hôte", 0xF0B52A);
        host.BindLocalPeer(2, HostSession.HostPlayerId);
        var client = new RemoteClient(clientNet, "Ami", 0x3366CC);
        hostNet.SimulateConnect();
        return (host, client);
    }

    private static void Pump(HostSession host, RemoteClient client, float seconds)
    {
        for (float t = 0; t < seconds; t += 0.05f) { host.Update(0.05); client.Update(0.05); }
    }

    [Fact]
    public void Client_JoinsTheLobby()
    {
        var (host, client) = Connect();
        Pump(host, client, 0.2f);
        Assert.Equal(2, client.PlayerId);
        Assert.Equal(2, host.Players.Count);
        Assert.Equal(2, client.Lobby.Count);
        Assert.Equal("Ami", host.Players[1].Name);
    }

    [Fact]
    public void Match_StartsOnBothSides_WithTheSameMapAndClans()
    {
        var (host, client) = Connect();
        Pump(host, client, 0.2f);
        host.StartMatch(new MatchSettings { Seed = 77, Mode = GameMode.Quick });
        Pump(host, client, 0.5f);
        var hostWorld = host.Match!.World;
        var clientWorld = client.World!;
        Assert.NotNull(clientWorld);
        Assert.Equal(MatchSettings.MaxClans, clientWorld.Armies.Count);
        Assert.Equal(hostWorld.Terrain.HeightAt(1234, 5678), clientWorld.Terrain.HeightAt(1234, 5678));
        Assert.Equal(hostWorld.Bubbles.Count, clientWorld.Bubbles.Count);
        // Le clan du client et sa position correspondent à ceux de l'hôte.
        var mine = clientWorld.FindArmyOfPlayer(2)!;
        var onHost = hostWorld.FindArmyOfPlayer(2)!;
        Assert.True(Vec2.Distance(mine.General.Position, onHost.General.Position) < 50);
        Assert.InRange(mine.Units.Count, onHost.Units.Count - 2, onHost.Units.Count + 2); // le dernier snapshot peut dater de 0,1 s
    }

    [Fact]
    public void ClientCommands_MoveItsOwnChiefOnTheHost()
    {
        var (host, client) = Connect();
        Pump(host, client, 0.2f);
        host.StartMatch(new MatchSettings { Seed = 78 });
        Pump(host, client, 0.3f);
        var onHost = host.Match!.World.FindArmyOfPlayer(2)!;
        var start = onHost.General.Position;
        client.Send(new DirectionCommand(0, 2, 1, 0));
        Pump(host, client, 2);
        Assert.True(Vec2.Distance(onHost.General.Position, start) > 50, "Le chef du client n'a pas bougé sur l'hôte");
        var onClient = client.World!.FindArmyOfPlayer(2)!;
        Assert.True(Vec2.Distance(onClient.General.Position, onHost.General.Position) < 60, "La copie du client ne suit pas");
    }

    [Fact]
    public void Snapshot_HidesEnemiesOutsideTheFog()
    {
        var (host, client) = Connect();
        Pump(host, client, 0.2f);
        host.StartMatch(new MatchSettings { Seed = 79, FogOfWar = true });
        Pump(host, client, 0.5f);
        var clientWorld = client.World!;
        // Les autres clans démarrent loin : le client ne doit pas voir leurs chefs.
        Assert.Contains(clientWorld.Armies, a => a.PlayerId != 2 && !a.ChiefVisible);
    }

    [Fact]
    public void Events_ReachTheClient()
    {
        var (host, client) = Connect();
        var received = new List<GameEvent>();
        client.EventReceived += received.Add;
        Pump(host, client, 0.2f);
        host.StartMatch(new MatchSettings { Seed = 80, AllowCheats = true });
        Pump(host, client, 0.2f);
        client.Send(new BuyCommand(0, 2, TroopKind.Warrior)); // pas assez de ressources au départ : un refus est annoncé
        Pump(host, client, 0.3f);
        Assert.Contains(received, e => e is BuyFailedEvent b && b.PlayerId == 2);
    }

    [Fact]
    public void DisconnectedPlayer_IsReplacedByABot()
    {
        var (hostLocal, _) = LocalTransport.CreatePair();
        var (hostNet, clientNet) = LocalTransport.CreatePair();
        var host = new HostSession(hostLocal, hostNet, "Hôte", 0xF0B52A);
        var client = new RemoteClient(clientNet, "Ami", 0x3366CC);
        hostNet.SimulateConnect();
        Pump(host, client, 0.2f);
        host.StartMatch(new MatchSettings { Seed = 81 });
        Pump(host, client, 0.2f);
        hostNet.SimulateDisconnect();
        Pump(host, client, 0.1f);
        var army = host.Match!.World.FindArmyOfPlayer(2)!;
        Assert.True(army.IsBot);
        Assert.NotNull(army.Brain);
    }
}
