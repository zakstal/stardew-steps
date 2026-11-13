# Stardew Steps

Walk in real life and your Stardew Valley farmer walks too. Each step your Android phone detects moves the farmer **one tile** in the direction of the movement key you're holding. Holding a key without walking does nothing.

```
Android phone ──Bluetooth──▶ bridge (Python, on Fedora) ──localhost:47821──▶ SMAPI mod in Stardew
```

Built against **Stardew Valley 1.6.15** and **SMAPI 4.5.2**.

## What you need

- Fedora with Bluetooth, Stardew Valley from Steam, SMAPI installed
- An Android phone (Android 8 or newer) with a step detector (nearly all phones have one)

## Build (on the Mac)

```bash
./scripts/package.sh
```

This creates `dist/`, which holds:

- `StardewSteps-mod.zip`: the mod
- `stardew-steps-bridge/`: the bridge
- `stardew-steps.apk`: the phone app

Copy `dist/` to the Fedora machine.

## Install on Fedora

1. **Mod.** Unzip `StardewSteps-mod.zip` into Stardew's `Mods` folder:
   ```bash
   unzip StardewSteps-mod.zip -d ~/.local/share/Steam/steamapps/common/Stardew\ Valley/Mods/
   ```
   If Steam is the Flatpak version, the folder is `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Stardew Valley/Mods/`.
2. **Bridge.**
   ```bash
   sudo dnf install python3-dbus python3-gobject   # usually already installed
   mkdir -p ~/stardew-steps && cp -R stardew-steps-bridge ~/stardew-steps/bridge
   ```
3. **Pair the phone.** Open Settings → Bluetooth on Fedora, turn on Bluetooth on the phone, and pair the two the normal way. You only do this once.

## Install on the phone

Copy `stardew-steps.apk` to the phone and open it. Allow "install unknown apps" when asked. Or install it over USB with `adb install stardew-steps.apk`.

When you first open it, allow **Physical activity**, **Nearby devices** (Bluetooth) and **Notifications**. Then allow it to ignore **battery optimisation**. Without that, many phones stop counting steps when the screen is off.

## Play

1. Start the bridge in a terminal on Fedora:
   ```bash
   cd ~/stardew-steps/bridge && python3 -m stardew_steps_bridge
   ```
   To have it start automatically instead:
   ```bash
   mkdir -p ~/.config/systemd/user
   cp ~/stardew-steps/bridge/stardew-steps-bridge.service ~/.config/systemd/user/
   systemctl --user daemon-reload && systemctl --user enable --now stardew-steps-bridge
   journalctl --user -u stardew-steps-bridge -f    # watch its log
   ```
2. Launch Stardew through Steam, with SMAPI as usual, and load your save. The SMAPI console shows `[Stardew Steps] Listening on 127.0.0.1:47821`, then `Bridge connected` once you take your first step.
3. On the phone, tap **Choose computer**, pick the Fedora machine, then tap **Connect**. The status should read "Connected to …".
4. Hold a direction key (WASD or arrows) and walk.

### How it behaves

- Each step walks the farmer one tile in the direction of the key you're holding. If you hold two keys, the one you pressed last wins.
- Steps taken while no key is held are thrown away.
- Up to 5 steps can wait in line. A step that's been waiting more than 2 seconds is dropped, so a laggy connection never sends the farmer off on a run after you've stopped.
- Steps don't count while a menu, dialogue or cutscene is open.
- A controller can't move the farmer on its own either.
- Settings live in `Mods/StardewSteps/config.json` (created on first run): `Port`, `QueueCapacity`, `MaxStepAgeSeconds`.

## Test without the phone

You can test the mod with fake steps; the bridge doesn't need Bluetooth for this:

```bash
python3 -m stardew_steps_bridge --fake          # press Enter = one step
python3 -m stardew_steps_bridge --fake-rate 2   # 2 steps per second
```

## Fedora + phone checklist

Things that could only be checked on the real setup. Each line says what to expect.

1. **Keys alone don't walk.** With the bridge stopped, hold a direction key in game. The farmer must not move.
2. **One step, one tile.** Run `--fake`, hold Right and press Enter once. The farmer walks exactly one tile and stops.
3. **No key, no step.** Press Enter without holding a key. Nothing happens.
4. **Last key wins.** Hold Up, then also hold Left, and press Enter. The farmer goes Left.
5. **Walls.** Hold a direction into a fence and press Enter. The farmer doesn't move, and the next step works normally.
6. **Menus.** Open the inventory and press Enter a few times, then close it. The farmer doesn't move afterwards.
7. **Everything else works.** Tools, clicking, talking to villagers and menus all behave normally.
8. **Controller.** If you have one plugged in, the stick doesn't move the farmer.
9. **Phone connects.** Stop `--fake`, start the normal bridge and connect from the phone. The bridge logs `phone connected`.
10. **Real walking.** Hold Right and take 10 steps. The farmer moves about 10 tiles.
11. **Screen off.** Lock the phone and walk while holding a key. The farmer still moves.
12. **Bluetooth drop.** Turn the phone's Bluetooth off, then on again. The app reconnects within about 10 seconds.
13. **Bridge restart.** Stop the bridge for about 40 seconds, then start it again. The phone reconnects by itself.

## Troubleshooting

- **The bridge says `org.freedesktop.DBus.Error.AccessDenied` on start.** Your user isn't allowed to register Bluetooth profiles. Run it once with `sudo` to confirm that's the cause, and tell Claude what you see.
- **The phone says "Can't reach …" forever.** Check that the bridge is running and the phone is paired with this computer. Then try unpairing and pairing again.
- **The farmer walks normally without steps.** Check that the SMAPI console lists `Stardew Steps` under loaded mods.

## Development

- Mod logic and tests: `dotnet test mod` (needs .NET 8 SDK; the mod targets `net6.0`)
- Bridge tests: `cd bridge && python3 -m unittest`
- Android tests: `cd android && ./gradlew testDebugUnitTest`
- In-game self-test (Debug build only): `STARDEW_STEPS_SELFTEST=<save folder> ./StardewModdingAPI` from the game folder. It loads that save, walks the farmer with scripted steps, logs `RESULT PASS/FAIL` lines, and quits.
