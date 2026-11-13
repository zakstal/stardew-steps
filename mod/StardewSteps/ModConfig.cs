namespace StardewSteps;

public sealed class ModConfig
{
    /// <summary>Local TCP port the bridge connects to.</summary>
    public int Port { get; set; } = 47821;

    /// <summary>Most steps that can wait in line; extras are dropped.</summary>
    public int QueueCapacity { get; set; } = 5;

    /// <summary>Steps older than this when their turn comes are dropped.</summary>
    public double MaxStepAgeSeconds { get; set; } = 2.0;
}
