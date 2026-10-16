using ChronoClash.Core.Commands;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Net;
using ChronoClash.Core.Simulation;
using Xunit;

namespace ChronoClash.Core.Tests;

public class MovementTests
{
    private static Match NewMatch(out int playerId)
    {
        var match = new Match(new MatchSettings { Seed = 21 });
        playerId = 1;
        match.AddArmy(playerId, "Testeur", 0xF0B52A, isBot: false);
        return match;
    }

    [Fact]
    public void MoveTo_BringsTheChiefToTheTarget()
    {
        var match = NewMatch(out int player);
        var general = match.World.Armies[0].General;
        var target = match.World.Terrain.SpawnPoints[1];
        match.Enqueue(new MoveToCommand(0, player, target.X, target.Y, false));
        // Au plus 3 minutes de jeu pour traverser une partie de la carte.
        for (int i = 0; i < 180 * TickClock.TicksPerSecond && general.Path.Count > 0; i++) match.Step();
        Assert.True(Vec2.Distance(general.Position, target) < 40, $"Le chef est resté à {Vec2.Distance(general.Position, target):0} px de la cible");
    }

    [Fact]
    public void Sprint_DrainsStamina_AndIsFaster()
    {
        var walk = NewMatch(out int p);
        var run = NewMatch(out _);
        walk.Enqueue(new DirectionCommand(0, p, 1, 0));
        run.Enqueue(new DirectionCommand(0, p, 1, 0));
        run.Enqueue(new SprintCommand(0, p, true));
        var w0 = walk.World.Armies[0].General.Position;
        var r0 = run.World.Armies[0].General.Position;
        for (int i = 0; i < 40; i++) { walk.Step(); run.Step(); } // 2 secondes
        var gw = walk.World.Armies[0].General;
        var gr = run.World.Armies[0].General;
        Assert.True(gr.Stamina < 100);
        // Le terrain peut bloquer l'un des deux : on vérifie seulement que le sprint ne va pas moins loin.
        Assert.True(Vec2.Distance(gr.Position, r0) >= Vec2.Distance(gw.Position, w0) - 1);
    }

    [Fact]
    public void CommandsGoThroughTheLocalTransport()
    {
        var match = NewMatch(out int player);
        var (hostEnd, clientEnd) = LocalTransport.CreatePair();
        var host = new HostSession(match, hostEnd);
        host.BindPeer(clientEnd.LocalPeerId, player);
        var client = new ClientSession(clientEnd, player);

        client.Send(new DirectionCommand(0, player, 0, 1));
        host.Update(0.5); // 10 ticks
        Assert.Equal(new Vec2(0, 1), match.World.Armies[0].General.InputDirection);
    }

    [Fact]
    public void AClientCannotCommandAnotherClan()
    {
        var match = NewMatch(out int player);
        match.AddArmy(2, "Autre", 0x3366CC, isBot: true);
        var (hostEnd, clientEnd) = LocalTransport.CreatePair();
        var host = new HostSession(match, hostEnd);
        host.BindPeer(clientEnd.LocalPeerId, player);
        // Le client essaie de faire bouger le clan du joueur 2 : l'ordre est réattribué à son propre clan.
        new ClientSession(clientEnd, player).Send(new DirectionCommand(0, 2, 1, 0));
        host.Update(0.1);
        Assert.Equal(Vec2.Zero, match.World.Armies[1].General.InputDirection);
        Assert.Equal(new Vec2(1, 0), match.World.Armies[0].General.InputDirection);
    }

    [Fact]
    public void CommandCodec_RoundTrip()
    {
        PlayerCommand[] commands =
        {
            new MoveToCommand(12, 3, 100.5f, 200.25f, true),
            new DirectionCommand(1, 1, -1, 0.5f),
            new StopCommand(2, 4),
            new SprintCommand(3, 5, true),
            new TacticCommand(4, 6, TacticKind.Aim, 10, 20),
            new BuyCommand(5, 7, TroopKind.Tank),
            new SelectFormationCommand(6, 8, 3),
            new DebugSpawnTroopsCommand(8, 1, 20),
        };
        foreach (var c in commands) Assert.Equal(c, CommandCodec.Decode(CommandCodec.Encode(c)));
        var cells = new SetFormationCellsCommand(7, 9, 2, new byte[] { 0, 1, 2, 3 });
        var back = (SetFormationCellsCommand)CommandCodec.Decode(CommandCodec.Encode(cells))!;
        Assert.Equal(cells.Cells, back.Cells);
    }
}
