using ChronoClash.Core.Map;
using ChronoClash.Core.Mathematics;
using ChronoClash.Core.Simulation;
using Xunit;

namespace ChronoClash.Core.Tests;

/// <summary>Tests de la génération de carte et des chemins. Une carte prend environ une seconde à générer.</summary>
public class MapTests
{
    [Fact]
    public void SameSeed_GivesSameTerrain()
    {
        var a = new World(new MatchSettings { Seed = 77 });
        var b = new World(new MatchSettings { Seed = 77 });
        for (int i = 0; i < 200; i++)
        {
            float x = i * 53 % Terrain.WorldSize, y = i * 97 % Terrain.WorldSize;
            Assert.Equal(a.Terrain.HeightAt(x, y), b.Terrain.HeightAt(x, y));
        }
        Assert.Equal(a.Terrain.Rocks.Count, b.Terrain.Rocks.Count);
        Assert.Equal(a.Terrain.SpawnPoints, b.Terrain.SpawnPoints);
    }

    [Theory]
    [InlineData(MapTheme.Temperate)]
    [InlineData(MapTheme.Archipelago)]
    [InlineData(MapTheme.Desert)]
    [InlineData(MapTheme.Winter)]
    public void EverySpawn_IsWalkable_AndReachableFromTheFirst(MapTheme theme)
    {
        var world = new World(new MatchSettings { Seed = 1234, Theme = theme });
        var spawns = world.Terrain.SpawnPoints;
        Assert.Equal(MatchSettings.MaxClans, spawns.Count);
        foreach (var s in spawns) Assert.True(world.Terrain.IsWalkable(s, 14), $"Départ dans un obstacle : {s}");
        for (int i = 1; i < spawns.Count; i++)
            Assert.NotNull(world.Pathfinder.Plan(spawns[0], spawns[i]));
    }

    [Fact]
    public void Path_NeverGoesThroughObstacles()
    {
        var world = new World(new MatchSettings { Seed = 5 });
        var from = world.Terrain.SpawnPoints[0];
        var to = world.Terrain.SpawnPoints[5];
        var path = world.Pathfinder.Plan(from, to);
        Assert.NotNull(path);
        var previous = from;
        foreach (var step in path!)
        {
            Assert.True(world.Pathfinder.LineClear(previous, step, 12), "Un segment du chemin traverse un obstacle");
            previous = step;
        }
    }
}
