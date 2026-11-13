namespace StardewSteps.Core;

/// <summary>Thread-safe queue of pending steps with a size cap and an age limit.</summary>
public sealed class StepQueue
{
    private readonly object _lock = new();
    private readonly Queue<DateTime> _items = new();
    private readonly int _capacity;
    private readonly TimeSpan _maxAge;

    public StepQueue(int capacity = 5, TimeSpan? maxAge = null)
    {
        _capacity = capacity;
        _maxAge = maxAge ?? TimeSpan.FromSeconds(2);
    }

    public int Count { get { lock (_lock) return _items.Count; } }

    /// <summary>Adds a step. Returns false (step dropped) when the queue is full.</summary>
    public bool Enqueue(DateTime receivedUtc)
    {
        lock (_lock)
        {
            if (_items.Count >= _capacity) return false;
            _items.Enqueue(receivedUtc);
            return true;
        }
    }

    /// <summary>Discards stale steps at the front, then takes one fresh step if any.</summary>
    public bool TryDequeueFresh(DateTime nowUtc)
    {
        lock (_lock)
        {
            while (_items.Count > 0)
            {
                var t = _items.Dequeue();
                if (nowUtc - t <= _maxAge) return true;
            }
            return false;
        }
    }

    public void Clear() { lock (_lock) _items.Clear(); }
}
