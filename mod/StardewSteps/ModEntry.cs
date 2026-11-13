using System.Net;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewSteps.Core;
using StardewValley;

namespace StardewSteps;

/// <summary>Gates walking so the farmer only moves one tile per real-life step.</summary>
public sealed class ModEntry : Mod
{
    private static readonly SButton[] GamepadMoveButtons =
    {
        SButton.LeftThumbstickUp, SButton.LeftThumbstickDown, SButton.LeftThumbstickLeft, SButton.LeftThumbstickRight,
        SButton.DPadUp, SButton.DPadDown, SButton.DPadLeft, SButton.DPadRight,
    };

    private ModConfig _config = new();
    private StepQueue _queue = new();
    private readonly DirectionTracker _tracker = new();
    private Walker _walker = null!;
    private StepServer? _server;

    public override void Entry(IModHelper helper)
    {
        _config = helper.ReadConfig<ModConfig>();
        _queue = new StepQueue(_config.QueueCapacity, TimeSpan.FromSeconds(_config.MaxStepAgeSeconds));
        _walker = new Walker(new FarmerDriver());

        _server = new StepServer(IPAddress.Loopback, _config.Port, () => _queue.Enqueue(DateTime.UtcNow), msg => Monitor.Log(msg, LogLevel.Info));
        try { _server.Start(); }
        catch (Exception ex) { Monitor.Log($"Couldn't listen on port {_config.Port}: {ex.Message}", LogLevel.Error); _server = null; }

        helper.Events.Input.ButtonPressed += OnButtonPressed;
        helper.Events.Input.ButtonReleased += OnButtonReleased;
        helper.Events.GameLoop.UpdateTicking += (_, _) => { if (Context.IsWorldReady) SuppressHeldMoveButtons(); };
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.GameLoop.SaveLoaded += (_, _) => ResetState();
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => ResetState();
        helper.Events.Player.Warped += (_, e) => { if (e.IsLocalPlayer) _walker.Cancel(); };
#if DEBUG
        SelfTest.Attach(helper, Monitor, _queue, _tracker, _walker);
#endif
        helper.Events.GameLoop.GameLaunched += (_, _) => AppDomain.CurrentDomain.ProcessExit += (_, _) => _server?.Stop();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady) return;
        var dir = KeyboardDirection(e.Button);
        if (dir is not null)
        {
            _tracker.Pressed(dir.Value);
            Helper.Input.Suppress(e.Button);
        }
        else if (Array.IndexOf(GamepadMoveButtons, e.Button) >= 0 && Context.IsPlayerFree)
        {
            Helper.Input.Suppress(e.Button);
        }
    }

    private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
    {
        var dir = KeyboardDirection(e.Button);
        if (dir is not null) _tracker.Released(dir.Value);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady) return;

        if (!Context.IsPlayerFree || !Context.CanPlayerMove)
        {
            // Menus, events, dialogue: steps don't count and nothing walks.
            _queue.Clear();
            _walker.Cancel();
            return;
        }

        if (_walker.Busy)
        {
            _walker.Tick();
            return;
        }

        if (_queue.TryDequeueFresh(DateTime.UtcNow) && _tracker.Current is { } dir)
            _walker.Start(dir);
    }

    /// <summary>Keep the game from seeing held movement keys/sticks, so it never walks on its own.</summary>
    private void SuppressHeldMoveButtons()
    {
        foreach (var b in AllKeyboardMoveButtons())
            if (Helper.Input.IsDown(b)) Helper.Input.Suppress(b);
        foreach (var b in GamepadMoveButtons)
            if (Helper.Input.IsDown(b)) Helper.Input.Suppress(b);
    }

    private static IEnumerable<SButton> AllKeyboardMoveButtons() =>
        Game1.options.moveUpButton.Concat(Game1.options.moveDownButton)
            .Concat(Game1.options.moveLeftButton).Concat(Game1.options.moveRightButton)
            .Select(b => b.ToSButton());

    private static Direction? KeyboardDirection(SButton button)
    {
        if (!button.TryGetKeyboard(out _)) return null;
        if (Game1.options.moveUpButton.Any(b => b.ToSButton() == button)) return Direction.Up;
        if (Game1.options.moveDownButton.Any(b => b.ToSButton() == button)) return Direction.Down;
        if (Game1.options.moveLeftButton.Any(b => b.ToSButton() == button)) return Direction.Left;
        if (Game1.options.moveRightButton.Any(b => b.ToSButton() == button)) return Direction.Right;
        return null;
    }

    private void ResetState()
    {
        _queue.Clear();
        _tracker.Reset();
        _walker.Cancel();
    }
}
