namespace StardewSteps.Core;

/// <summary>Walks the farmer one tile per Start(), driven by Tick() once per game update.</summary>
public sealed class Walker
{
    private const int MaxTicks = 240; // safety net: never walk longer than ~4s for one step
    private readonly IFarmerDriver _farmer;
    private readonly int _blockedTicks;
    private readonly Func<Direction?>? _next;
    private Direction _dir;
    private (float x, float y) _target;
    private (float x, float y) _lastPos;
    private int _stillTicks;
    private int _ticks;

    /// <param name="next">Asked on arrival for the next step's direction; if it gives one,
    /// the walk continues without halting so the walking animation doesn't stutter.</param>
    public Walker(IFarmerDriver farmer, int blockedTicks = 10, Func<Direction?>? next = null)
    {
        _farmer = farmer;
        _blockedTicks = blockedTicks;
        _next = next;
    }

    public bool Busy { get; private set; }

    public void Start(Direction d)
    {
        var (x, y) = _farmer.Position;
        _dir = d;
        _target = TileMath.TargetCenter(x, y, d);
        _lastPos = (x, y);
        _stillTicks = 0;
        _ticks = 0;
        Busy = true;
        _farmer.Move(d);
    }

    public void Tick()
    {
        if (!Busy) return;
        var pos = _farmer.Position;

        if (Reached(pos))
        {
            _farmer.Snap(_target.x, _target.y);
            if (_next?.Invoke() is { } nextDir)
            {
                Start(nextDir);
                return;
            }
            _farmer.Stop();
            Busy = false;
            return;
        }

        _stillTicks = pos == _lastPos ? _stillTicks + 1 : 0;
        _lastPos = pos;
        if (_stillTicks >= _blockedTicks || ++_ticks >= MaxTicks)
        {
            Cancel();
            return;
        }

        _farmer.Move(_dir); // keep the game's movement flag set every tick
    }

    public void Cancel()
    {
        if (Busy) _farmer.Stop();
        Busy = false;
    }

    private bool Reached((float x, float y) p) => _dir switch
    {
        Direction.Up => p.y <= _target.y,
        Direction.Down => p.y >= _target.y,
        Direction.Left => p.x <= _target.x,
        _ => p.x >= _target.x,
    };
}
