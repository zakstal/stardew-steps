using StardewSteps.Core;
using Xunit;

namespace StardewSteps.Core.Tests;

public class DirectionTrackerTests
{
    [Fact]
    public void MostRecentHeldWins()
    {
        var d = new DirectionTracker();
        d.Pressed(Direction.Up);
        d.Pressed(Direction.Left);
        Assert.Equal(Direction.Left, d.Current);
        d.Released(Direction.Left);
        Assert.Equal(Direction.Up, d.Current);
        d.Released(Direction.Up);
        Assert.Null(d.Current);
    }

    [Fact]
    public void RepressMovesToFront()
    {
        var d = new DirectionTracker();
        d.Pressed(Direction.Up);
        d.Pressed(Direction.Left);
        d.Pressed(Direction.Up);
        Assert.Equal(Direction.Up, d.Current);
    }

    [Fact]
    public void ResetClears()
    {
        var d = new DirectionTracker();
        d.Pressed(Direction.Down);
        d.Reset();
        Assert.Null(d.Current);
    }
}
