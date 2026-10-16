using ChronoClash.Core.Commands;
using ChronoClash.Core.Data;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Formations;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using ChronoClash.Core.Systems;
using Xunit;

namespace ChronoClash.Core.Tests;

/// <summary>Tests de l'étape M2 : données, formations, bulles, achats et déplacement des troupes.</summary>
public class ArmyTests
{
    private static Match NewMatch(bool cheats = false)
    {
        var match = new Match(new MatchSettings { Seed = 33, AllowCheats = cheats });
        match.AddArmy(1, "Testeur", 0xF0B52A, isBot: false);
        return match;
    }

    [Fact]
    public void GameData_LoadsEveryTroopAndEra()
    {
        var data = GameData.Default;
        Assert.Equal(8, data.Eras.Count);
        Assert.Equal(8, data.Chiefs.Count);
        Assert.Equal("Mammouth", data.Stats(TroopKind.Tank, 0).Name);
        Assert.Equal(12, data.Troop(TroopKind.Warrior).Cost);
        Assert.Equal(1800, data.Eras[7].Threshold);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Layout_GivesASlotToEveryTroop(int preset)
    {
        var cells = FormationTemplate.Preset(preset).Cells;
        foreach (var counts in new[] { new[] { 5, 3, 1 }, new[] { 60, 30, 10 }, new[] { 300, 120, 40 } })
        {
            var layout = FormationLayout.Compute(cells, counts, GameData.Default);
            for (int k = 0; k < 3; k++) Assert.Equal(counts[k], layout.SlotsByKind[k].Count);
        }
    }

    [Fact]
    public void Layout_GrowsWithTheArmy()
    {
        var cells = FormationTemplate.Preset(0).Cells;
        var small = FormationLayout.Compute(cells, new[] { 10, 5, 2 }, GameData.Default);
        var big = FormationLayout.Compute(cells, new[] { 400, 200, 60 }, GameData.Default);
        Assert.True(big.Scale > small.Scale);
    }

    [Fact]
    public void Layout_Line_PutsTanksInFrontAndRangedBehind()
    {
        var layout = FormationLayout.Compute(FormationTemplate.Preset(0).Cells, new[] { 20, 10, 4 }, GameData.Default);
        float tankForward = layout.SlotsByKind[2].Average(s => s.X);
        float rangedForward = layout.SlotsByKind[1].Average(s => s.X);
        Assert.True(tankForward > 0 && rangedForward < 0);
    }

    [Fact]
    public void Layout_EmptyDrawing_FallsBackToLine()
    {
        var layout = FormationLayout.Compute(new byte[FormationTemplate.CellCount], new[] { 6, 3, 1 }, GameData.Default);
        Assert.Equal(6, layout.SlotsByKind[0].Count);
    }

    [Fact]
    public void TouchingABubble_GivesATroopAndResources()
    {
        var match = NewMatch();
        var army = match.World.Armies[0];
        var bubble = new Bubble(9999, BubbleKind.Tank, army.General.Position, 16, 0);
        match.World.Bubbles.Add(bubble);
        int resources = army.Resources, units = army.Units.Count;
        match.Step();
        Assert.DoesNotContain(bubble, match.World.Bubbles);
        Assert.Equal(units + 1, army.Units.Count);
        Assert.Equal(TroopKind.Tank, army.Units[^1].Kind);
        Assert.Equal(resources + GameData.Default.Troop(TroopKind.Tank).Gain, army.Resources);
    }

    [Fact]
    public void Buying_NeedsEnoughResources()
    {
        var match = NewMatch();
        var army = match.World.Armies[0];
        army.Resources = 5;
        match.Enqueue(new BuyCommand(0, 1, TroopKind.Warrior));
        match.Step();
        Assert.Equal(5, army.Resources);
        Assert.Contains(match.Events, e => e is Events.BuyFailedEvent);

        army.Resources = 40;
        int before = army.Units.Count;
        match.Enqueue(new BuyCommand(0, 1, TroopKind.Ranged));
        match.Step();
        Assert.Equal(40 - GameData.Default.Troop(TroopKind.Ranged).Cost, army.Resources);
        Assert.Equal(before + 1, army.Units.Count);
    }

    [Fact]
    public void DebugTroops_OnlyWhenCheatsAreAllowed()
    {
        var normal = NewMatch(cheats: false);
        normal.Enqueue(new DebugSpawnTroopsCommand(0, 1, 20));
        normal.Step();
        Assert.True(normal.World.Armies[0].Units.Count < 5);

        var test = NewMatch(cheats: true);
        int before = test.World.Armies[0].Units.Count;
        test.Enqueue(new DebugSpawnTroopsCommand(0, 1, 20));
        test.Step();
        Assert.True(test.World.Armies[0].Units.Count >= before + 20);
    }

    [Fact]
    public void Troops_GatherAroundTheirSlots()
    {
        var match = NewMatch(cheats: true);
        var army = match.World.Armies[0];
        match.World.Bubbles.Clear(); // pour que le nombre de troupes ne change pas pendant le test
        match.Enqueue(new DebugSpawnTroopsCommand(0, 1, 40));
        for (int i = 0; i < 10 * TickClock.TicksPerSecond; i++) match.Step(); // 10 secondes
        float average = army.Units.Average(u => Vec2.Distance(u.Position, FormationSystem.SlotWorldPosition(army, u.Slot)));
        Assert.True(average < 40, $"Les troupes sont en moyenne à {average:0} px de leur place");
    }

    [Fact]
    public void SetFormationCells_RejectsInvalidDrawings()
    {
        var match = NewMatch();
        var army = match.World.Armies[0];
        var before = (byte[])army.Formations[0].Cells.Clone();
        match.Enqueue(new SetFormationCellsCommand(0, 1, 0, new byte[] { 1, 2, 3 }));      // mauvaise taille
        var bad = new byte[FormationTemplate.CellCount]; bad[0] = 9;
        match.Enqueue(new SetFormationCellsCommand(0, 1, 0, bad));                        // valeur inconnue
        match.Step();
        Assert.Equal(before, army.Formations[0].Cells);
    }
}
