using ChronoClash.Core.Commands;
using ChronoClash.Core.Entities;
using ChronoClash.Core.Events;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using ChronoClash.Core.Systems;
using Xunit;

namespace ChronoClash.Core.Tests;

/// <summary>Tests de l'étape M3 : combat, récompenses, succession, élimination, tactiques et ères.</summary>
public class CombatTests
{
    private static Match NewMatch(out Army player)
    {
        var match = new Match(new MatchSettings { Seed = 44, AllowCheats = true });
        player = match.AddArmy(1, "Testeur", 0xF0B52A, isBot: false)!;
        match.World.Bubbles.Clear(); // les bulles fausseraient les comptes
        return match;
    }

    private static void Run(Match match, float seconds)
    {
        for (int i = 0; i < seconds * TickClock.TicksPerSecond; i++) match.Step();
    }

    [Theory]
    [InlineData(0, 0, 1)] [InlineData(0, 1, 2)] [InlineData(1, 4, 8)] [InlineData(5, 2, 1)]
    public void Underdog_DoublesPerEraOfGap(int killerEra, int victimEra, int expected)
        => Assert.Equal(expected, CombatRules.UnderdogMultiplier(killerEra, victimEra));

    [Fact]
    public void KillingATroop_GivesPointsAndResources()
    {
        var match = NewMatch(out var player);
        var world = match.World;
        var enemy = world.CreateArmy(2, "Ennemi", 0x3366CC, isBot: true, player.General.Position + new Vec2(3000, 0));
        var victim = Recruitment.AddUnit(world, enemy, TroopKind.Tank, enemy.General.Position);
        int points = player.Points, resources = player.Resources;
        CombatRules.ApplyDamage(world, player, victim, 100000, sourceEra: 0);
        match.Step();
        Assert.True(victim.Dead);
        Assert.DoesNotContain(victim, enemy.Units);
        Assert.Equal(points + world.Data.Troop(TroopKind.Tank).Points, player.Points);
        Assert.Equal(resources + world.Data.Tuning.Combat.ResourcesFor(TroopKind.Tank), player.Resources);
    }

    [Fact]
    public void KillingAMoreAdvancedEnemy_MultipliesThePoints()
    {
        var match = NewMatch(out var player);
        var world = match.World;
        var enemy = world.CreateArmy(2, "Ennemi", 0x3366CC, isBot: true, player.General.Position + new Vec2(3000, 0));
        enemy.Era = 2;
        var victim = Recruitment.AddUnit(world, enemy, TroopKind.Warrior, enemy.General.Position);
        int before = player.Points;
        CombatRules.ApplyDamage(world, player, victim, 100000, sourceEra: 0);
        match.Step();
        int baseValue = (int)MathF.Round(1 * (1 + 2 * world.Data.Tuning.Combat.VictimEraPointBonus));
        Assert.Equal(before + baseValue * 4, player.Points); // 2 ères d'écart = ×4
    }

    [Fact]
    public void DeadChief_IsReplacedByTheNearestTroop()
    {
        var match = NewMatch(out var player);
        var world = match.World;
        Recruitment.SpawnTestTroops(world, player, 5);
        int units = player.Units.Count;
        CombatRules.ApplyDamage(world, null, player.General, 100000, 0);
        match.Step();
        Assert.False(player.General.Dead);
        Assert.True(player.General.Promoted);
        Assert.True(player.Alive);
        Assert.Equal(units - 1, player.Units.Count);
        Assert.Contains(match.Events, e => e is ChiefPromotedEvent);
    }

    [Fact]
    public void ArmyWithoutChiefOrTroops_IsEliminated_AndTheKillerGetsTheBonus()
    {
        var match = NewMatch(out var player);
        var world = match.World;
        var enemy = world.CreateArmy(2, "Ennemi", 0x3366CC, isBot: true, player.General.Position + new Vec2(3000, 0));
        int before = player.Points;
        CombatRules.ApplyDamage(world, player, enemy.General, 100000, 0);
        match.Step();
        Assert.False(enemy.Alive);
        Assert.Contains(match.Events, e => e is ArmyEliminatedEvent);
        Assert.Equal(before + world.Data.Tuning.Combat.ChiefPoints + world.Data.Tuning.Combat.EliminationPoints, player.Points);
    }

    [Fact]
    public void Armies_FightOnTheirOwnWhenClose()
    {
        var match = NewMatch(out var player);
        match.Enqueue(new DebugSpawnTroopsCommand(0, 1, 30));
        match.Enqueue(new DebugSpawnEnemyCommand(0, 1, 30, 0));
        match.Step();
        var enemy = match.World.Armies[1];
        // On envoie le chef vers l'ennemi : les deux armées doivent se battre.
        match.Enqueue(new MoveToCommand(0, 1, enemy.General.Position.X, enemy.General.Position.Y, false));
        int before = player.Units.Count + enemy.Units.Count;
        Run(match, 40);
        Assert.True(player.Units.Count + enemy.Units.Count < before, "Aucune troupe n'est morte en 40 s de combat");
    }

    [Fact]
    public void Charge_LaunchesFrontTroops_ThenNeedsACooldown()
    {
        var match = NewMatch(out var player);
        match.Enqueue(new DebugSpawnTroopsCommand(0, 1, 30));
        Run(match, 3);
        match.Enqueue(new TacticCommand(0, 1, TacticKind.Charge));
        match.Step();
        Assert.Contains(player.Units, u => u.ChargeTime > 0);
        Assert.True(player.TacticCooldowns[(int)TacticKind.Charge] > 0);
        match.Enqueue(new TacticCommand(0, 1, TacticKind.Charge));
        match.Step();
        Assert.Contains(match.Events, e => e is TacticFailedEvent);
    }

    [Fact]
    public void WarCry_BoostsNearbyTroops()
    {
        var match = NewMatch(out var player);
        match.Enqueue(new DebugSpawnTroopsCommand(0, 1, 10));
        Run(match, 3);
        match.Enqueue(new TacticCommand(0, 1, TacticKind.WarCry));
        match.Step();
        var boosted = player.Units.First(u => u.WarCryTime > 0);
        Assert.True(CombatRules.DamageMultiplier(match.World, boosted) > 1.2f);
    }

    [Fact]
    public void ReachingTheThreshold_EvolvesTheWholeArmyIn20Seconds()
    {
        var match = NewMatch(out var player);
        match.Enqueue(new DebugSpawnTroopsCommand(0, 1, 20));
        match.Step();
        player.Points = match.World.Data.Eras[1].Threshold;
        match.Step();
        Assert.NotNull(player.Evolution);
        Run(match, 10);
        Assert.Equal(0, player.Era);                                   // à mi-chemin : encore l'ancienne ère…
        Assert.Contains(player.Units, u => u.Era == 1);                // … mais certaines troupes ont déjà évolué
        Run(match, 11);
        Assert.Equal(1, player.Era);
        Assert.All(player.Units, u => Assert.Equal(1, u.Era));
        Assert.Equal(1, player.General.Era);
        Assert.Equal(match.World.Data.Chiefs[1].Hp, player.General.MaxHp);
    }
}
