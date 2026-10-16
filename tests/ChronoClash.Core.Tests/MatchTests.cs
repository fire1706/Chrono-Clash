using ChronoClash.Core.Simulation;
using Xunit;

namespace ChronoClash.Core.Tests;

public class MatchTests
{
    [Fact]
    public void TwentyTicks_IsOneSecond()
    {
        var match = new Match(new MatchSettings());
        for (int i = 0; i < TickClock.TicksPerSecond; i++) match.Step();
        Assert.Equal(1.0, match.World.TimeSeconds, 6);
    }

    [Fact]
    public void Advance_ConvertsRealTimeIntoFixedTicks()
    {
        var match = new Match(new MatchSettings());
        int ticks = match.Advance(0.26); // 0,26 s = 5 ticks complets de 0,05 s
        Assert.Equal(5, ticks);
        Assert.Equal(5, match.World.Tick);
    }

    [Fact]
    public void Advance_LimitsCatchUpAfterAFreeze()
    {
        var clock = new TickClock();
        Assert.Equal(clock.MaxCatchUpTicks, clock.Advance(30));
    }
}
