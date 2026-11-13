using StardewSteps.Core;
using Xunit;

namespace StardewSteps.Core.Tests;

public class StepQueueTests
{
    [Fact]
    public void DropsBeyondCapacity()
    {
        var q = new StepQueue(5);
        var t = DateTime.UtcNow;
        for (int i = 0; i < 5; i++) Assert.True(q.Enqueue(t));
        Assert.False(q.Enqueue(t));
        Assert.Equal(5, q.Count);
    }

    [Fact]
    public void StaleStepsDiscarded()
    {
        var q = new StepQueue();
        var t = DateTime.UtcNow;
        q.Enqueue(t);
        Assert.False(q.TryDequeueFresh(t.AddSeconds(2.1)));
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void FreshStepTaken()
    {
        var q = new StepQueue();
        var t = DateTime.UtcNow;
        q.Enqueue(t);
        Assert.True(q.TryDequeueFresh(t.AddSeconds(1.9)));
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void StaleFrontSkippedFreshBehindTaken()
    {
        var q = new StepQueue();
        var t = DateTime.UtcNow;
        q.Enqueue(t);
        q.Enqueue(t.AddSeconds(1.5));
        Assert.True(q.TryDequeueFresh(t.AddSeconds(2.5)));
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void BurstIsCappedAndStaleDropped()
    {
        var q = new StepQueue();
        var t = DateTime.UtcNow;
        for (int i = 0; i < 10; i++) q.Enqueue(t);
        Assert.Equal(5, q.Count);
        Assert.False(q.TryDequeueFresh(t.AddSeconds(3)));
        Assert.Equal(0, q.Count);
    }

    [Fact]
    public void ClearEmpties()
    {
        var q = new StepQueue();
        q.Enqueue(DateTime.UtcNow);
        q.Clear();
        Assert.Equal(0, q.Count);
    }
}
