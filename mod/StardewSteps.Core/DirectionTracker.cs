namespace StardewSteps.Core;

/// <summary>Tracks held movement keys; the most recently pressed one still held wins.</summary>
public sealed class DirectionTracker
{
    private readonly List<Direction> _held = new();

    public Direction? Current => _held.Count == 0 ? null : _held[^1];

    public void Pressed(Direction d)
    {
        _held.Remove(d);
        _held.Add(d);
    }

    public void Released(Direction d) => _held.Remove(d);

    public void Reset() => _held.Clear();

    /// <summary>Forget keys that are no longer down (e.g. released while the window was unfocused).</summary>
    public void Prune(Func<Direction, bool> isDown) => _held.RemoveAll(d => !isDown(d));
}
