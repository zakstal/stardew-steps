#if DEBUG
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewSteps.Core;
using StardewValley;
using StardewValley.Menus;
using Farm = StardewValley.Farm;

namespace StardewSteps;

/// <summary>
/// Debug-only scripted check. Set STARDEW_STEPS_SELFTEST=&lt;save folder name&gt; before launching SMAPI:
/// loads that save, feeds steps without a phone/keyboard, logs RESULT lines, then quits.
/// </summary>
internal sealed class SelfTest
{
    private readonly IMonitor _log;
    private readonly StepQueue _queue;
    private readonly DirectionTracker _tracker;
    private readonly Walker _walker;
    private readonly string _save;
    private int _phase = -1;
    private int _wait;
    private (float x, float y) _start;
    private readonly FarmerDriver _farmer = new();

    private SelfTest(IMonitor log, StepQueue q, DirectionTracker t, Walker w, string save)
    { _log = log; _queue = q; _tracker = t; _walker = w; _save = save; }

    public static void Attach(IModHelper helper, IMonitor log, StepQueue q, DirectionTracker t, Walker w)
    {
        var save = Environment.GetEnvironmentVariable("STARDEW_STEPS_SELFTEST");
        if (string.IsNullOrEmpty(save)) return;
        ModEntry.AssumeKeysDown = true;
        var test = new SelfTest(log, q, t, w, save);
        helper.Events.GameLoop.UpdateTicked += test.OnTick;
    }

    private void Result(string name, bool ok, string detail) =>
        _log.Log($"RESULT {(ok ? "PASS" : "FAIL")} {name}: {detail}", ok ? LogLevel.Info : LogLevel.Error);

    private void OnTick(object? sender, UpdateTickedEventArgs e)
    {
        if (_phase == -1)
        {
            if (Game1.activeClickableMenu is TitleMenu && e.Ticks > 120)
            {
                _log.Log($"SelfTest: loading save {_save}", LogLevel.Info);
                SaveGame.Load(_save);
                Game1.exitActiveMenu();
                _phase = 0;
            }
            return;
        }
        if (!Context.IsWorldReady) return;
        if (_wait > 0) { _wait--; return; }

        var pos = _farmer.Position;
        switch (_phase)
        {
            case 0: // let the day start settle, close any morning menus/dialogue
                if (!Context.IsPlayerFree) { Game1.exitActiveMenu(); Game1.dialogueUp = false; return; }
                if (Game1.currentLocation is not Farm)
                {
                    var door = Game1.getFarm().GetMainFarmHouseEntry();
                    Game1.warpFarmer("Farm", door.X, door.Y + 3, 2);
                    _wait = 120; return;
                }
                _wait = 60; _phase = 1; return;
            case 1:
                _start = pos; _tracker.Reset();
                _queue.Enqueue(DateTime.UtcNow); // no key held -> must be dropped
                _wait = 60; _phase = 2; return;
            case 2:
                Result("step without key does nothing", pos == _start, $"start={_start} now={pos}");
                _start = pos; _tracker.Pressed(Direction.Right);
                _queue.Enqueue(DateTime.UtcNow);
                _wait = 90; _phase = 3; return;
            case 3:
                Result("one step right = one tile", Math.Abs(pos.x - _start.x - 64) < 0.5 && Math.Abs(pos.y - _start.y) < 0.5 || IsAligned(pos) && pos.x > _start.x && pos.x - _start.x <= 96,
                    $"start={_start} now={pos} walkerBusy={_walker.Busy}");
                _start = pos; _tracker.Released(Direction.Right); _tracker.Pressed(Direction.Left);
                for (int i = 0; i < 3; i++) _queue.Enqueue(DateTime.UtcNow);
                _wait = 200; _phase = 4; return;
            case 4:
                var dx = _start.x - pos.x;
                Result("three queued steps left = three tiles (or blocked earlier)", IsAligned(pos) && dx >= 0 && dx <= 192 && dx % 64 == 0,
                    $"start={_start} now={pos} moved={dx}px");
                _tracker.Reset();
                _phase = 5; _wait = 30; return;
            case 5:
                _log.Log("SelfTest: done, quitting", LogLevel.Info);
                Game1.quit = true;
                Environment.Exit(0);
                return;
        }
    }

    private static bool IsAligned((float x, float y) p) => p.x % 64 == 32 && p.y % 64 == 32;
}
#endif
