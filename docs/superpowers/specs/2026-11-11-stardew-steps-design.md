# Stardew Steps — Design

| Field | Value |
|---|---|
| Status | Draft — waiting for Zak's review |
| Version | 0.1 |
| Date | 2026-11-11 |
| Owner | Zak |
| Related task (ClickUp) | — |
| Applies to / Implements | `~/projects/stardew-steps` (phone app, Linux bridge, SMAPI mod) |
| Revision | Initial draft |

## Intent

Zak wants his real-life walking to drive his Stardew Valley farmer: **one real step taken with his Android phone = the farmer walks one step (one tile) in the game.** Direction still comes from the keyboard. Keep it very simple.

## How it fits together

```
Android phone ──Bluetooth (classic RFCOMM/SPP)──▶ Fedora: bridge (Python) ──TCP 127.0.0.1──▶ SMAPI mod in Stardew
  step detector                                     relays "step" lines                       walks farmer 1 tile
```

Three pieces, one repo:

1. **`mod/`** — SMAPI mod (C#), runs inside Stardew on Fedora.
2. **`bridge/`** — Python 3 program on Fedora; accepts the phone over Bluetooth, forwards each step to the mod.
3. **`android/`** — Kotlin Android app; detects each step, sends it over Bluetooth.

## Behavior (agreed with Zak)

- Normal walking is **blocked**. Holding a movement key (WASD/arrows/whatever the game's move bindings are) does not move the farmer by itself.
- Each step from the phone makes the farmer walk **one tile** in the direction of the movement key currently held, then stop.
- A step that arrives (or comes up in the queue) while **no movement key is held is dropped**. Steps do not bank up for later.
- If steps arrive faster than the farmer can walk them, they **queue** and the farmer walks them off in a row.
- Everything else is untouched: menus, tools, talking, mouse, horse-free actions all work normally. Only walking is gated.
- Phone app: bare-bones screen — pick paired device, connect button, connection status, step counter. Keeps working with the screen off.
- Bridge: small background program on Fedora.

## Goals

- One phone step → one tile of walking, with walking animation, respecting collisions.
- Works on Fedora (Steam install, SMAPI 4.x already installed), current Stardew 1.6.x.
- Survives disconnects: phone, bridge, and mod each reconnect on their own without restarting the game.

## Non-goals

- Choosing direction from the phone.
- Step history, stats, daily totals, cloud sync.
- iOS, Windows/Mac gameplay support, Play Store release (sideloaded APK is fine).
- Multiplayer / split-screen correctness.
- Horse riding behavior (my call: gating applies to the player's walking; horse is not specially handled).

## Anti-goals (would be a failure even if it "works")

- The farmer moving when no real steps happened.
- A burst of delayed steps making the farmer run off unexpectedly long after Zak stopped walking (see queue cap below).
- Breaking menus, tools, cutscenes, or other controls.
- Requiring root on the phone, or system-wide changes on Fedora beyond normal Bluetooth pairing.

## Constraints

- **Claude cannot reach the Fedora machine.** Everything is built on Zak's Mac; Zak copies it over and tests. So: clear install steps, minimal dependencies on Fedora, and a way to test each piece without the others.
- Fedora has Bluetooth (assumed — Zak to confirm). Phone pairs with it once via normal settings.
- The Mac has Stardew Valley installed via Steam (no SMAPI yet), Python 3, Java. No .NET SDK, no Android SDK, no adb. The builder installs what's needed on the Mac (via Homebrew): .NET SDK for SMAPI 4 mods, Android command-line tools/SDK + Gradle wrapper. Installing SMAPI on the Mac's Stardew is allowed and useful — it gives build references and lets the mod be tested locally with a fake step sender.

## Decided details (my calls — Zak may override)

### Mod
- C#, SMAPI 4 standard project (`Pathoschild.Stardew.ModBuildConfig`). Output: a zip/folder Zak drops into `Stardew Valley/Mods/` on Fedora.
- Listens on `127.0.0.1:47821` (configurable in `config.json`). Plain TCP, newline-delimited text; each line `step` = one step, `ping` = keepalive (ignored). One client at a time; accepts a new connection if the old one drops.
- Network thread only enqueues; all game-state changes happen on the game's update tick.
- Queue cap: **max 5 pending steps**; extras are dropped (protects against delayed bursts). Configurable.
- Direction = the movement key held at the moment a queued step *starts* (re-read for every queued step). If none held, that step is dropped. If two keys are held, the most recently pressed one wins — no diagonal steps. (my call)
- A step walks to the center of the next tile in that direction. If the farmer starts mid-tile, it walks to the center of the adjacent tile, so it always ends tile-aligned. (my call)
- Steps older than 2 seconds when they reach the front of the queue are dropped. This stops a stalled connection from delivering a batch of old steps. (my call)
- Direction comes from the keyboard only. Gamepad/thumbstick movement is gated too, so it can't bypass the steps, but it doesn't supply a direction. (my call)
- A step that is blocked by a wall/object is consumed and the farmer doesn't move.
- The farmer ends each step aligned on a tile.
- Steps are ignored when the player can't move (menu open, cutscene/event, dialogue, not in a world).
- On-screen: nothing by default. SMAPI console logs connect/disconnect.

### Bridge
- Python 3, standard library + `python3-dbus`/`python3-gobject` if needed (both normally on Fedora Workstation). No pip installs.
- Registers a Serial Port Profile service (UUID `00001101-0000-1000-8000-00805F9B34FB`) with BlueZ so the phone can connect by UUID. Preferred method: BlueZ D-Bus `ProfileManager1.RegisterProfile`. (Avoid `sdptool`, which needs bluetoothd compat mode.)
- Phone sends `ping` every 10 s. The bridge closes a phone connection that's been silent for 30 s, so a half-dead link can't block reconnects. One phone at a time; a new connection replaces the old. (my call)
- Forwards each `step` line from the phone to the mod's TCP port. If the game isn't running/listening, steps are dropped (not buffered); bridge keeps retrying the mod connection every few seconds.
- Run from a terminal; also ship an optional systemd **user** unit to auto-start.
- Test mode: `--fake` flag sends one step per Enter key press (or `--fake-rate N` steps/sec) to the mod, no phone needed.

### Android app
- Kotlin, single activity + foreground service (with persistent notification) so it keeps running with screen off.
- Uses `Sensor.TYPE_STEP_DETECTOR` (one event per step). Needs `ACTIVITY_RECOGNITION` permission; Bluetooth `BLUETOOTH_CONNECT` (Android 12+).
- User picks Fedora machine from the list of already-paired devices; app remembers it and auto-reconnects.
- Sends `step\n` per detected step over an RFCOMM socket to the SPP UUID.
- Asks the user to exempt the app from battery optimisation (needed on many phones for screen-off step detection). (my call)
- minSdk 26. Output: debug-signed APK Zak sideloads.
- Known limit: Android's step detector can lag a little or occasionally deliver steps in small bursts; the mod's queue cap bounds the effect.

## Testing

- Build the mod against the Mac's Stardew 1.6.x + SMAPI 4.x; Fedora should be on the same versions (Steam keeps both current). README notes the versions it was built against.
- Mod: test on the Mac with SMAPI installed and the bridge in `--fake` mode (bridge's Bluetooth part not needed for fake mode, so `--fake` must work without BlueZ).
- Bridge Bluetooth path and phone app: only testable by Zak on Fedora + phone. Provide a short manual test checklist.
- Deliverables include a README with install steps for each piece on Fedora/phone.

## Left to the builder

- Exact technique for gating movement and walking one tile (e.g. suppress move inputs and drive the farmer toward the next tile center each tick vs. letting held input through until the tile boundary is crossed), as long as the behavior above holds, animation looks normal, and collisions are respected.
- Walking vs running speed (use whatever the game's current run/walk setting gives).
- Project layout inside each folder, logging details, app UI styling.
