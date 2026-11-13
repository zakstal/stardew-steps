using StardewSteps.Core;
using Xunit;

namespace StardewSteps.Core.Tests;

public class ReviewFixTests
{
    [Theory]
    [InlineData(true, false, true)]   // walking around: suppress
    [InlineData(false, false, false)] // menu/dialogue: leave input alone
    [InlineData(true, true, false)]   // arcade minigame: leave input alone
    public void SuppressOnlyWhenWalkingFree(bool playerFree, bool inMinigame, bool expected) =>
        Assert.Equal(expected, InputPolicy.SuppressMoveInput(playerFree, inMinigame));

    [Fact]
    public void PruneDropsKeysNoLongerDown()
    {
        var d = new DirectionTracker();
        d.Pressed(Direction.Up);
        d.Pressed(Direction.Right); // release of Right was missed
        d.Prune(dir => dir == Direction.Up);
        Assert.Equal(Direction.Up, d.Current);
        d.Prune(_ => false);
        Assert.Null(d.Current);
    }

    private sealed class Driver : IFarmerDriver
    {
        public (float x, float y) Position { get; set; }
        public Direction? Moving;
        public int StopCalls;
        public void Move(Direction d) => Moving = d;
        public void Stop() { Moving = null; StopCalls++; }
        public void Snap(float x, float y) => Position = (x, y);
        public void GameTick()
        {
            if (Moving is null) return;
            var (x, y) = Position;
            Position = Moving == Direction.Right ? (x + 4, y) : Moving == Direction.Left ? (x - 4, y) : Moving == Direction.Down ? (x, y + 4) : (x, y - 4);
        }
    }

    [Fact]
    public void ChainedStepsDontHaltBetweenTiles()
    {
        var f = new Driver { Position = (32, 32) };
        int remaining = 2;
        var w = new Walker(f, next: () => remaining-- > 0 ? Direction.Right : null);
        w.Start(Direction.Right);
        for (int i = 0; i < 500 && w.Busy; i++) { f.GameTick(); w.Tick(); }
        Assert.Equal((224f, 32f), f.Position); // 3 tiles
        Assert.Equal(1, f.StopCalls);          // halted once, at the very end
    }
}
