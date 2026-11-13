namespace StardewSteps.Core;

/// <summary>Seam between the walk logic and the actual game farmer.</summary>
public interface IFarmerDriver
{
    /// <summary>Center of the farmer's bounding box, in pixels.</summary>
    (float x, float y) Position { get; }
    void Move(Direction d);
    void Stop();
    /// <summary>Place the farmer so its bounding-box center is at (x, y).</summary>
    void Snap(float x, float y);
}
