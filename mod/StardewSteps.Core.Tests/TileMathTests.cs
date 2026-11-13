using StardewSteps.Core;
using Xunit;

namespace StardewSteps.Core.Tests;

public class TileMathTests
{
    [Fact] public void TargetFromTileCenter() => Assert.Equal((96f, 32f), TileMath.TargetCenter(32, 32, Direction.Right));
    [Fact] public void TargetFromMidTile() => Assert.Equal((96f, 32f), TileMath.TargetCenter(50, 32, Direction.Right));
    [Fact] public void TargetUp() => Assert.Equal((160f, 96f), TileMath.TargetCenter(150, 140, Direction.Up));
    [Fact] public void TargetLeft() => Assert.Equal((96f, 160f), TileMath.TargetCenter(150, 140, Direction.Left));
    [Fact] public void TargetDown() => Assert.Equal((160f, 224f), TileMath.TargetCenter(150, 140, Direction.Down));
}
