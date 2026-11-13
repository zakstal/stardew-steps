# Stardew Steps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One real step detected by an Android phone makes the Stardew Valley farmer walk exactly one tile in the keyboard-held direction.

**Architecture:** Kotlin Android app sends `step`/`ping` lines over classic Bluetooth RFCOMM (SPP UUID) to a Python bridge on Fedora, which relays `step` lines over TCP `127.0.0.1:47821` to a SMAPI mod. The mod gates normal walking and walks one tile per queued step. Pure logic (queue, direction, protocol) lives in testable units, separate from game/OS glue.

**Tech Stack:** C# / .NET 6 / SMAPI 4 (`Pathoschild.Stardew.ModBuildConfig`), xUnit; Python 3 stdlib + `dbus-python` + `PyGObject` (Fedora system packages), `unittest`; Kotlin / Android Gradle Plugin, minSdk 26, JUnit 4.

**Spec:** `docs/superpowers/specs/2026-11-11-stardew-steps-design.md`

## Global Constraints

- Mod TCP: `127.0.0.1:47821`, configurable in `config.json`; newline-delimited text; `step` = one step, `ping` = keepalive (ignored); one client at a time, new connection replaces old.
- Queue cap **5** pending steps (configurable); extras dropped. Steps older than **2 s** at the front of the queue dropped (configurable).
- Direction: re-read at the moment each queued step *starts*; most recently pressed held move key wins; no diagonals; none held → step dropped. Keyboard only supplies direction; gamepad/thumbstick movement is gated too.
- Step = walk to center of the adjacent tile in that direction; always ends tile-aligned. Blocked step is consumed, farmer doesn't move.
- Steps ignored (dropped) when player can't move (menu, event, dialogue, no world).
- Network thread only enqueues; game state touched only on the update tick.
- Bridge: no pip installs; SPP UUID `00001101-0000-1000-8000-00805F9B34FB` registered via BlueZ D-Bus `ProfileManager1.RegisterProfile` (no `sdptool`); mod unreachable → drop steps, retry every few seconds; phone silent **30 s** → close; `--fake` (Enter = one step) and `--fake-rate N` work without BlueZ; optional systemd **user** unit.
- Phone: sends `step\n` per `Sensor.TYPE_STEP_DETECTOR` event, `ping\n` every **10 s**; foreground service; permissions `ACTIVITY_RECOGNITION`, `BLUETOOTH_CONNECT` (API 31+); pick from paired devices, remember, auto-reconnect; ask for battery-optimisation exemption; minSdk 26; debug-signed APK.
- Non-goals: phone-chosen direction, stats/history, iOS, Play Store, multiplayer, special horse handling.
- Claude cannot reach the Fedora machine: everything built and (where possible) tested on the Mac.

## Review Focus

1. **Key released mid-step** → farmer still finishes walking to the target tile center (no half-tile stop). Test: `WalkerTests.ReleasingKeyMidStepStillReachesTarget` (Task 4).
2. **Mod restarted / game not running while bridge runs** → bridge keeps retrying and drops steps meanwhile, never crashes or buffers. Test: `test_relay_drops_steps_while_mod_down_then_recovers` (Task 3).
3. **Garbage or partial lines on TCP** (e.g. `st` + `ep\n` split across packets, `\r\n`, unknown words) → split packets reassembled, CRLF tolerated, unknown lines ignored. Test: `LineParserTests.*` (Task 2).
4. **Burst after stall** (10 steps arriving at once after 3 s silence) → at most 5 queued, stale ones dropped. Test: `StepQueueTests.BurstIsCappedAndStaleDropped` (Task 1).
5. **Phone reconnects while old link half-dead** → new connection replaces old immediately. Test: `test_new_phone_connection_replaces_old` (Task 3).

---

## File Structure

```
mod/
  StardewSteps.sln
  StardewSteps.Core/            # pure logic, net6.0, no game refs
    StepQueue.cs                # cap + age expiry
    DirectionTracker.cs         # most-recent held key
    LineParser.cs               # byte stream -> lines
    Direction.cs                # enum Up/Down/Left/Right
    TileMath.cs                 # target tile center from pixel pos
  StardewSteps.Core.Tests/      # xUnit
  StardewSteps/                 # SMAPI mod
    ModEntry.cs                 # wiring, events
    StepServer.cs               # TCP listener thread
    Walker.cs                   # per-tick one-tile walk state machine (pure-ish, IFarmerDriver seam)
    FarmerDriver.cs             # IFarmerDriver over Game1.player
    ModConfig.cs
    manifest.json
bridge/
  stardew_steps_bridge/
    __main__.py                 # CLI: --fake, --fake-rate, --mod-port
    relay.py                    # ModRelay: TCP client w/ retry
    phone.py                    # PhoneSession: line reading, ping timeout, replace-old
    bluez.py                    # RegisterProfile + NewConnection fd handoff
  tests/test_relay.py, tests/test_phone.py
  stardew-steps-bridge.service  # systemd user unit
android/                        # Gradle project, app module
  app/src/main/java/com/stardewsteps/
    MainActivity.kt, StepService.kt, BtLink.kt, StepProtocol.kt
  app/src/test/java/com/stardewsteps/StepProtocolTest.kt
README.md
```

---

### Task 1: Toolchain + core queue and direction logic

**Files:**
- Create: `mod/StardewSteps.sln`, `mod/StardewSteps.Core/{StardewSteps.Core.csproj,Direction.cs,StepQueue.cs,DirectionTracker.cs}`, `mod/StardewSteps.Core.Tests/{StardewSteps.Core.Tests.csproj,StepQueueTests.cs,DirectionTrackerTests.cs}`, `.gitignore`

**Interfaces:**
- Produces:
  - `enum Direction { Up, Down, Left, Right }`
  - `class StepQueue(int capacity = 5, TimeSpan? maxAge = null /*2s*/)`: `bool Enqueue(DateTime receivedUtc)` (false if full → dropped), `bool TryDequeueFresh(DateTime nowUtc)` (discards stale fronts, true if a fresh step was taken), `void Clear()`, `int Count`
  - `class DirectionTracker`: `void Pressed(Direction d)`, `void Released(Direction d)`, `Direction? Current` (most recently pressed still held), `void Reset()`

- [ ] **Step 1:** Install toolchain: `brew install --cask dotnet-sdk` (need .NET 6+ SDK; SMAPI 4 mods target `net6.0`). Verify `dotnet --list-sdks` shows ≥6. Create sln + classlib (`net6.0`) + xunit test project.
- [ ] **Step 2: Write failing tests**

```csharp
[Fact] public void DropsBeyondCapacity() { var q=new StepQueue(5); var t=DateTime.UtcNow;
  for(int i=0;i<5;i++) Assert.True(q.Enqueue(t)); Assert.False(q.Enqueue(t)); Assert.Equal(5,q.Count); }
[Fact] public void StaleStepsDiscarded() { var q=new StepQueue(); var t=DateTime.UtcNow;
  q.Enqueue(t); Assert.False(q.TryDequeueFresh(t.AddSeconds(2.1))); Assert.Equal(0,q.Count); }
[Fact] public void FreshStepTaken() { var q=new StepQueue(); var t=DateTime.UtcNow;
  q.Enqueue(t); Assert.True(q.TryDequeueFresh(t.AddSeconds(1.9))); }
[Fact] public void BurstIsCappedAndStaleDropped() { var q=new StepQueue(); var t=DateTime.UtcNow;
  for(int i=0;i<10;i++) q.Enqueue(t); Assert.Equal(5,q.Count);
  Assert.False(q.TryDequeueFresh(t.AddSeconds(3))); Assert.Equal(0,q.Count); }

[Fact] public void MostRecentHeldWins() { var d=new DirectionTracker(); d.Pressed(Direction.Up); d.Pressed(Direction.Left);
  Assert.Equal(Direction.Left,d.Current); d.Released(Direction.Left); Assert.Equal(Direction.Up,d.Current);
  d.Released(Direction.Up); Assert.Null(d.Current); }
[Fact] public void RepressMovesToFront() { var d=new DirectionTracker(); d.Pressed(Direction.Up); d.Pressed(Direction.Left);
  d.Pressed(Direction.Up); Assert.Equal(Direction.Up,d.Current); }
```

- [ ] **Step 3:** Run `dotnet test mod/` → FAIL (types missing).
- [ ] **Step 4:** Implement. `StepQueue` is thread-safe (lock) — enqueued from the network thread, dequeued on the game tick. `DirectionTracker` keeps an ordered list of held directions.
- [ ] **Step 5:** `dotnet test mod/` → PASS.
- [ ] **Step 6:** Commit `feat(mod): step queue and direction tracker`.

### Task 2: Line parser + TCP step server

**Files:**
- Create: `mod/StardewSteps.Core/LineParser.cs`, `mod/StardewSteps.Core.Tests/LineParserTests.cs`, `mod/StardewSteps.Core/StepServer.cs`, `mod/StardewSteps.Core.Tests/StepServerTests.cs`

(StepServer has no game deps, so it lives in Core for testability; the mod project just uses it.)

**Interfaces:**
- Consumes: `StepQueue.Enqueue`
- Produces:
  - `class LineParser`: `IEnumerable<string> Feed(ReadOnlySpan<byte> chunk)` — returns completed lines, trimmed of `\r`, ASCII.
  - `class StepServer(IPAddress addr, int port, Action onStep, Action<string> log)`: `void Start()`, `void Stop()`, `int BoundPort`. Calls `onStep` for each `step` line; ignores `ping` and unknown lines; accepting a new client closes the previous one.

- [ ] **Step 1: Failing tests**

```csharp
[Fact] public void ReassemblesSplitLines() { var p=new LineParser();
  Assert.Empty(p.Feed("st"u8)); Assert.Equal(new[]{"step"}, p.Feed("ep\n"u8)); }
[Fact] public void ToleratesCrlfAndMultiple() { var p=new LineParser();
  Assert.Equal(new[]{"step","ping","step"}, p.Feed("step\r\nping\nstep\n"u8)); }

// StepServerTests: port 0, real TcpClient
[Fact] public async Task CountsOnlyStepLines()   // send "step\nping\nhello\nstep\n" → onStep called 2x within 1s
[Fact] public async Task NewClientReplacesOld()  // client A connects, client B connects; A's stream reads EOF; B's "step\n" counted
```

- [ ] **Step 2:** `dotnet test mod/` → FAIL.
- [ ] **Step 3:** Implement. Background thread with `TcpListener`; per-client read loop; `Stop()` closes listener and client and joins thread.
- [ ] **Step 4:** `dotnet test mod/` → PASS.
- [ ] **Step 5:** Commit `feat(mod): TCP step server`.

### Task 3: Bridge relay, phone session, fake mode

**Files:**
- Create: `bridge/stardew_steps_bridge/{__init__.py,__main__.py,relay.py,phone.py}`, `bridge/tests/{test_relay.py,test_phone.py}`

**Interfaces:**
- Produces:
  - `relay.ModRelay(host="127.0.0.1", port=47821, retry_s=3.0)`: `send_step() -> bool` (False = dropped because mod down; never raises), `close()`. Reconnect attempted at most every `retry_s`.
  - `phone.PhoneHub(on_step: Callable[[], None], idle_timeout_s=30.0)`: `attach(sock) -> None` (closes any previous session's socket, then reads lines in a thread), `close()`.
  - CLI: `python3 -m stardew_steps_bridge [--mod-port 47821] [--fake | --fake-rate N]`.

- [ ] **Step 1: Failing tests** (`unittest`, real sockets on localhost / `socket.socketpair()`):

```python
def test_relay_sends_step_line(self): # fake TCP server on port 0 receives b"step\n"
def test_relay_drops_steps_while_mod_down_then_recovers(self):
    r = ModRelay(port=free_port, retry_s=0.1); self.assertFalse(r.send_step())
    start server on free_port; time.sleep(0.2); self.assertTrue(r.send_step())  # server got b"step\n"
def test_phone_step_lines_call_on_step(self):  # socketpair; write b"step\nping\nstep\n" → on_step called 2x
def test_phone_idle_timeout_closes(self):      # idle_timeout_s=0.2; no data → peer recv returns b"" within 1s
def test_new_phone_connection_replaces_old(self):  # attach(a); attach(b) → a's peer reads EOF; b still works
```

- [ ] **Step 2:** `cd bridge && python3 -m unittest -v` → FAIL.
- [ ] **Step 3:** Implement. Phone sessions use `sock.settimeout(idle_timeout_s)`; timeout → close. `__main__` in fake mode wires stdin Enter / a timer straight to `ModRelay.send_step()` and imports nothing from `bluez.py`.
- [ ] **Step 4:** `python3 -m unittest -v` → PASS.
- [ ] **Step 5:** Commit `feat(bridge): mod relay, phone sessions, fake mode`.

### Task 4: SMAPI mod — gating and one-tile walk

**Files:**
- Create: `mod/StardewSteps.Core/TileMath.cs`, `mod/StardewSteps.Core/Walker.cs`, `mod/StardewSteps.Core.Tests/{TileMathTests.cs,WalkerTests.cs}`, `mod/StardewSteps/{StardewSteps.csproj,ModEntry.cs,FarmerDriver.cs,ModConfig.cs,manifest.json}`

**Interfaces:**
- Consumes: `StepQueue`, `DirectionTracker`, `StepServer`, `Direction`.
- Produces:
  - `TileMath.TargetCenter(float x, float y, Direction d, int tile = 64) -> (float x, float y)` — center of the tile adjacent (in `d`) to the tile containing `(x,y)`. Use the farmer's bounding-box center as the position the game reports via `IFarmerDriver.Position`.
  - `interface IFarmerDriver { (float x,float y) Position {get;} void Move(Direction d); void Stop(); void Snap(float x, float y); }`
  - `class Walker(IFarmerDriver f, int blockedTicks = 10)`: `bool Busy`, `void Start(Direction d)`, `void Tick()` — moves each tick; on reaching/crossing target along axis → `Snap` + `Stop`, not busy; if position unchanged for `blockedTicks` ticks → `Stop`, not busy (step consumed).
  - `ModConfig { int Port = 47821; int QueueCapacity = 5; double MaxStepAgeSeconds = 2.0; }`

- [ ] **Step 1: Failing tests** with a fake driver that advances 4 px per tick in the moved direction (or 0 when "walled"):

```csharp
[Fact] public void TargetFromTileCenter() => Assert.Equal((96f,32f), TileMath.TargetCenter(32,32,Direction.Right));
[Fact] public void TargetFromMidTile()    => Assert.Equal((96f,32f), TileMath.TargetCenter(50,32,Direction.Right));
[Fact] public void WalksExactlyOneTileAndSnaps()  // start (32,32) Right; tick until !Busy; Position==(96,32); Stop called
[Fact] public void ReleasingKeyMidStepStillReachesTarget() // Walker has no key input; after Start it completes regardless — assert reaches (96,32)
[Fact] public void BlockedStepIsConsumed()   // walled driver; after 10 ticks !Busy, Position unchanged
```

- [ ] **Step 2:** `dotnet test mod/` → FAIL.
- [ ] **Step 3:** Implement `TileMath` and `Walker`. → `dotnet test mod/` PASS.
- [ ] **Step 4:** Install SMAPI on the Mac's Stardew (latest SMAPI 4.x installer from smapi.io; game at `~/Library/Application Support/Steam/steamapps/common/Stardew Valley`). Create the mod csproj with `Pathoschild.Stardew.ModBuildConfig`; `dotnet build mod/StardewSteps` succeeds and deploys to the Mac's `Mods/StardewSteps`.
- [ ] **Step 5:** Implement the mod glue:
  - `FarmerDriver`: `Move` → `Game1.player.SetMovingUp/Down/Left/Right(true)` for `d` (others false); `Stop` → `Game1.player.Halt()`; `Snap` sets position so the bounding-box center equals target.
  - `ModEntry`: start `StepServer` (onStep → `queue.Enqueue(DateTime.UtcNow)`). `ButtonPressed`/`ButtonReleased`: map `Game1.options.moveUp/Down/Left/RightButton` (via `ToSButton()`) to `DirectionTracker`, and **suppress** those buttons plus `LeftThumbstick*` and `DPad*`. `UpdateTicking`: also suppress held move buttons every tick (`Helper.Input.Suppress`). `UpdateTicked`: if `!Context.IsPlayerFree || !Context.CanPlayerMove` → `queue.Clear()`, `walker` stop, return; else if walker busy → `Tick()`; else if `queue.TryDequeueFresh(now)` → if `tracker.Current` is null drop, else `walker.Start(dir)`. `ReturnedToTitle`/`SaveLoaded` → reset tracker/queue. Dispose server on `GameLoop` exit.
  - `manifest.json`: `UniqueID` `zak.StardewSteps`, `MinimumApiVersion` `4.0.0`.
- [ ] **Step 6: Manual test on Mac.** Launch Stardew via SMAPI, load a save. Run `cd bridge && python3 -m stardew_steps_bridge --fake`. Check:
  1. Holding a move key with no steps → farmer doesn't move.
  2. Hold Right + press Enter once → walks exactly one tile, ends centered.
  3. Press Enter with no key → nothing.
  4. Hold Up+Left (Left pressed last) → goes Left.
  5. Walk into a fence → no movement, next step works.
  6. Open the inventory, press Enter → nothing queued afterwards.
  7. `--fake-rate 6` while holding a key → smooth continuous walk; stop the bridge → farmer stops within ~1 tile.
  8. Tools, menus, mouse clicks work normally.
  If gating fails (e.g. the game still moves on held keys), fix it here (e.g. also zero `Game1.player.movementDirections` before the walker runs).
- [ ] **Step 7:** Commit `feat(mod): gate walking, one tile per step`.

### Task 5: Bridge Bluetooth (BlueZ profile)

**Files:**
- Create: `bridge/stardew_steps_bridge/bluez.py`, `bridge/stardew-steps-bridge.service`; Modify: `bridge/stardew_steps_bridge/__main__.py`

**Interfaces:**
- Consumes: `PhoneHub.attach(sock)`, `ModRelay.send_step`.
- Produces: `bluez.serve(on_connection: Callable[[socket.socket], None]) -> NoReturn` — registers profile object path `/stardewsteps/spp` with `ProfileManager1.RegisterProfile(path, "00001101-0000-1000-8000-00805F9B34FB", {"Name": "Stardew Steps", "Role": "server", "Channel": dbus.UInt16(1), "RequireAuthentication": False, "RequireAuthorization": False})`, runs a GLib main loop, and in `NewConnection(device, fd, props)` does `socket.socket(fileno=os.dup(fd.take()))` → `on_connection`.

- [ ] **Step 1:** Implement; default CLI mode (no `--fake`) calls `bluez.serve(hub.attach)`. Import `dbus`/`gi` lazily so fake mode and unit tests run on the Mac.
- [ ] **Step 2:** On the Mac: `python3 -m py_compile bridge/stardew_steps_bridge/*.py` passes; `python3 -m unittest -v` still passes. (Real Bluetooth is only testable on Fedora; covered by README checklist.)
- [ ] **Step 3:** systemd user unit: `ExecStart=/usr/bin/python3 -m stardew_steps_bridge`, `WorkingDirectory=%h/stardew-steps/bridge`, `Restart=on-failure`.
- [ ] **Step 4:** Commit `feat(bridge): BlueZ SPP profile`.

### Task 6: Android app

**Files:**
- Create: `android/` Gradle project (wrapper, `settings.gradle.kts`, `app/build.gradle.kts`, `AndroidManifest.xml`), `app/src/main/java/com/stardewsteps/{MainActivity.kt,StepService.kt,BtLink.kt,StepProtocol.kt}`, `app/src/test/java/com/stardewsteps/StepProtocolTest.kt`

**Interfaces:**
- Produces:
  - `object StepProtocol { val STEP: ByteArray /* "step\n" */; val PING: ByteArray /* "ping\n" */; const val PING_INTERVAL_MS = 10_000L; val SPP_UUID: UUID }`
  - `class BtLink(device: BluetoothDevice)`: `connect()` / `send(bytes)` / `close()`; on failure the service retries with backoff 1 s → 10 s max.
  - `StepService` (foreground, type `health` on API 34+): registers `TYPE_STEP_DETECTOR`; each event → `link.send(STEP)`, increments count broadcast to UI; pings every 10 s.
  - `MainActivity`: paired-device picker (saved in SharedPreferences), Connect/Disconnect, status text, step count; requests `ACTIVITY_RECOGNITION`, `BLUETOOTH_CONNECT` (31+), `POST_NOTIFICATIONS` (33+), and battery-optimisation exemption via `ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS`.

- [ ] **Step 1:** Install toolchain: `brew install --cask android-commandlinetools temurin@17`; `sdkmanager "platforms;android-35" "build-tools;35.0.0" "platform-tools"`; set `ANDROID_HOME`.
- [ ] **Step 2: Failing test**

```kotlin
@Test fun stepLine() = assertEquals("step\n", String(StepProtocol.STEP))
@Test fun pingLine() = assertEquals("ping\n", String(StepProtocol.PING))
@Test fun sppUuid() = assertEquals("00001101-0000-1000-8000-00805f9b34fb", StepProtocol.SPP_UUID.toString())
```

- [ ] **Step 3:** `cd android && ./gradlew testDebugUnitTest` → FAIL; implement `StepProtocol` → PASS.
- [ ] **Step 4:** Implement service, link, activity. Socket: `device.createRfcommSocketToServiceRecord(SPP_UUID)`.
- [ ] **Step 5:** `./gradlew assembleDebug lint` succeeds; APK at `android/app/build/outputs/apk/debug/app-debug.apk`.
- [ ] **Step 6:** Commit `feat(android): step sender app`.

### Task 7: README, packaging, Fedora checklist

**Files:**
- Create: `README.md`, `scripts/package.sh`

- [ ] **Step 1:** `scripts/package.sh` builds the mod in Release and zips `dist/StardewSteps-mod.zip`, copies `dist/stardew-steps-bridge/` and `dist/stardew-steps.apk`. Run it; all three exist.
- [ ] **Step 2:** README covers: what it does; Fedora install (`sudo dnf install python3-dbus python3-gobject` if missing; unzip mod into `~/.local/share/Steam/steamapps/common/Stardew Valley/Mods/`; copy bridge to `~/stardew-steps/bridge`; optional `systemctl --user enable --now stardew-steps-bridge`); pairing the phone in GNOME Settings; installing the APK; the Stardew/SMAPI versions built against; `--fake` test; the Fedora + phone checklist (bridge logs "phone connected"; walking 10 steps while holding Right moves ~10 tiles; screen off still works; turning Bluetooth off and on reconnects within ~10 s; killing the bridge for 40 s then restarting reconnects).
- [ ] **Step 3:** Commit `docs: install and test guide`.
