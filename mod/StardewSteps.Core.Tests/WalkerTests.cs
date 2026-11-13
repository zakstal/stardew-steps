using StardewSteps.Core;
using Xunit;

namespace StardewSteps.Core.Tests;

public class WalkerTests
{
    private sealed class FakeDriver : IFarmerDriver
    {
        public (float x, float y) Position { get; set; }
        public Direction? Moving;
        public bool Walled;
        public int StopCalls;

        public void Move(Direction d) => Moving = d;
        public void Stop() { Moving = null; StopCalls++; }
        public void Snap(float x, float y) => Position = (x, y);

        /// <summary>Simulates the game moving the farmer 4px this tick.</summary>
        public void GameTick()
        {
            if (Moving is null || Walled) return;
            var (x, y) = Position;
            Position = Moving switch
            {
                Direction.Up => (x, y - 4),
                Direction.Down => (x, y + 4),
                Direction.Left => (x - 4, y),
                _ => (x + 4, y),
            };
        }
    }

    private static int RunUntilIdle(Walker w, FakeDriver f, int max = 500)
    {
        int ticks = 0;
        while (w.Busy && ticks < max) { f.GameTick(); w.Tick(); ticks++; }
        return ticks;
    }

    [Fact]
    public void WalksExactlyOneTileAndSnaps()
    {
        var f = new FakeDriver { Position = (32, 32) };
        var w = new Walker(f);
        w.Start(Direction.Right);
        Assert.True(w.Busy);
        RunUntilIdle(w, f);
        Assert.False(w.Busy);
        Assert.Equal((96f, 32f), f.Position);
        Assert.Null(f.Moving);
        Assert.True(f.StopCalls >= 1);
    }

    [Fact]
    public void SnapsWhenOvershooting()
    {
        var f = new FakeDriver { Position = (34, 30) }; // 4px ticks will overshoot 96
        var w = new Walker(f);
        w.Start(Direction.Right);
        RunUntilIdle(w, f);
        Assert.Equal((96f, 32f), f.Position);
    }

    [Fact]
    public void ReleasingKeyMidStepStillReachesTarget()
    {
        // Walker takes no key input after Start: a released key can't stop it short.
        var f = new FakeDriver { Position = (32, 32) };
        var w = new Walker(f);
        w.Start(Direction.Down);
        f.GameTick(); w.Tick();
        Assert.True(w.Busy);
        RunUntilIdle(w, f);
        Assert.Equal((32f, 96f), f.Position);
    }

    [Fact]
    public void BlockedStepIsConsumed()
    {
        var f = new FakeDriver { Position = (32, 32), Walled = true };
        var w = new Walker(f, blockedTicks: 10);
        w.Start(Direction.Left);
        var ticks = RunUntilIdle(w, f);
        Assert.False(w.Busy);
        Assert.Equal(10, ticks);
        Assert.Equal((32f, 32f), f.Position);
        Assert.Null(f.Moving);
    }

    [Fact]
    public void CancelStopsImmediately()
    {
        var f = new FakeDriver { Position = (32, 32) };
        var w = new Walker(f);
        w.Start(Direction.Right);
        w.Cancel();
        Assert.False(w.Busy);
        Assert.Null(f.Moving);
    }
}
