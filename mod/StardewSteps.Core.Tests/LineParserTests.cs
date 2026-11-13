using StardewSteps.Core;
using Xunit;

namespace StardewSteps.Core.Tests;

public class LineParserTests
{
    [Fact]
    public void ReassemblesSplitLines()
    {
        var p = new LineParser();
        Assert.Empty(p.Feed("st"u8));
        Assert.Equal(new[] { "step" }, p.Feed("ep\n"u8));
    }

    [Fact]
    public void ToleratesCrlfAndMultiple()
    {
        var p = new LineParser();
        Assert.Equal(new[] { "step", "ping", "step" }, p.Feed("step\r\nping\nstep\n"u8));
    }
}
