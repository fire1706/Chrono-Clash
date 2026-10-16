using ChronoClash.Core.Simulation;
using Xunit;

namespace ChronoClash.Core.Tests;

public class SimRandomTests
{
    [Fact]
    public void SameSeed_GivesSameSequence()
    {
        var a = new SimRandom(1234);
        var b = new SimRandom(1234);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void DifferentSeeds_GiveDifferentSequences()
    {
        Assert.NotEqual(new SimRandom(1).NextUInt(), new SimRandom(2).NextUInt());
    }

    [Fact]
    public void NextDouble_StaysBetweenZeroAndOne()
    {
        var r = new SimRandom(99);
        for (int i = 0; i < 10000; i++)
        {
            double v = r.NextDouble();
            Assert.True(v >= 0.0 && v < 1.0);
        }
    }

    [Fact]
    public void IntRange_RespectsBounds()
    {
        var r = new SimRandom(7);
        for (int i = 0; i < 10000; i++) Assert.InRange(r.Range(3, 8), 3, 7);
    }

    [Fact]
    public void MatchesHtmlPrototype()
    {
        // Valeurs produites par le générateur du prototype HTML (mulberry32) avec la graine 1.
        var r = new SimRandom(1);
        Assert.Equal(2693262067u, r.NextUInt());
    }
}
