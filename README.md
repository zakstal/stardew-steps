# Stardew Steps

Walk in real life and your Stardew Valley farmer walks too. Each step your Android phone counts moves the farmer **one tile** in the direction of the arrow key (or WASD key) you're holding. Holding a key without walking does nothing.

```
Android phone ──Bluetooth──▶ bridge program on your Linux PC ──▶ Stardew Steps mod inside Stardew Valley
```

There are three pieces, and you set up each one once:

| Piece | Where it goes | File to download |
|---|---|---|
| The mod | Stardew Valley's `Mods` folder on the PC | `StardewSteps-mod.zip` |
| The bridge | Any folder on the PC | `stardew-steps-bridge.zip` |
| The phone app | Your Android phone | `stardew-steps.apk` |

All three files are on the **[Releases page](https://github.com/zakstal/stardew-steps/releases/latest)**.

Built and tested against Stardew Valley **1.6.15** and SMAPI **4.5.2**. The PC instructions are written for **Fedora**, but any Linux with Bluetooth should work.

---

## What you need

- A Linux PC with Bluetooth, and Stardew Valley installed from Steam
- An Android phone running Android 8 or newer (almost every phone has the step sensor this needs)
- About 20 minutes for the first setup

---

## Part 1: Set up the PC

### Step 1. Install SMAPI (skip if you already play with mods)

SMAPI is the standard mod loader for Stardew Valley.

1. Run Stardew Valley once from Steam and quit. This makes sure the game files are in place.
2. Download the latest SMAPI from **https://smapi.io** (the big "Download SMAPI" button).
3. Unzip it. In a terminal, go into the unzipped folder and run:
   ```bash
   bash "install on Linux.sh"
   ```
4. Answer the questions: pick a color scheme, then choose **Install**. SMAPI finds the game on its own.
5. Start Stardew Valley from Steam as usual. A black console window should open next to the game. That console means SMAPI is working. Quit the game.

### Step 2. Install the mod

1. Download `StardewSteps-mod.zip` from the [Releases page](https://github.com/zakstal/stardew-steps/releases/latest).
2. Find the game folder. In Steam, right-click **Stardew Valley**, then **Manage**, then **Browse local files**. A `Mods` folder sits inside it.
3. Unzip the mod into that `Mods` folder, so you end up with `Mods/StardewSteps/manifest.json`. From a terminal:
   ```bash
   unzip ~/Downloads/StardewSteps-mod.zip -d ~/.local/share/Steam/steamapps/common/Stardew\ Valley/Mods/
   ```
   That path is for Steam installed from Fedora's packages. If you use the Flatpak Steam, the game folder is `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Stardew Valley/` instead.

### Step 3. Install the bridge

The bridge is a small Python program. It receives steps from your phone over Bluetooth and passes them to the game.

1. Install the two system packages it needs. They're usually already there on Fedora:
   ```bash
   sudo dnf install python3-dbus python3-gobject
   ```
   On Ubuntu or Debian, use `sudo apt install python3-dbus python3-gi` instead.
2. Download `stardew-steps-bridge.zip` from the [Releases page](https://github.com/zakstal/stardew-steps/releases/latest) and unzip it into a folder called `~/stardew-steps/bridge`:
   ```bash
   mkdir -p ~/stardew-steps
   unzip ~/Downloads/stardew-steps-bridge.zip -d ~/stardew-steps
   mv ~/stardew-steps/stardew-steps-bridge ~/stardew-steps/bridge
   ```

### Step 4. Check the PC side works (no phone needed)

The bridge has a fake mode that sends steps when you press Enter. Use it to check the mod before adding the phone.

1. Start Stardew Valley and load your farm.
2. Look for this line in the SMAPI console:
   ```
   [Stardew Steps] Listening on 127.0.0.1:47821
   ```
3. Open a terminal and run:
   ```bash
   cd ~/stardew-steps/bridge && python3 -m stardew_steps_bridge --fake
   ```
4. Click back into the game. **Hold the right arrow key.** The farmer should stay still.
5. Keep holding the key, click the terminal, and press **Enter** once. The farmer walks exactly one tile to the right.

If both of those happened, the PC side is working. Press `Ctrl+C` in the terminal to stop fake mode.

---

## Part 2: Set up the phone

### Step 5. Pair the phone with the PC

You do this once.

1. On the PC, open **Settings → Bluetooth** and turn Bluetooth on. Leave that window open, because the PC can only be found while it's open.
2. On the phone, open **Settings → Connected devices → Pair new device**. The wording varies by phone.
3. Tap your PC's name when it appears.
4. Confirm that the code on both screens matches and accept on both.

### Step 6. Install the phone app

The app isn't on the Play Store, so you install it directly.

1. On the phone, open the [Releases page](https://github.com/zakstal/stardew-steps/releases/latest) in the browser and tap `stardew-steps.apk` to download it.
2. Open the downloaded file. If Android says the browser isn't allowed to install apps, tap **Settings**, turn on **Allow from this source**, then go back.
3. Tap **Install**. If Play Protect warns about an unknown app, tap **Install anyway**.

### Step 7. Give the app its permissions

Open **Stardew Steps** on the phone. It asks for these:

| Permission | Why it's needed | Choose |
|---|---|---|
| Physical activity | to count your steps | Allow |
| Nearby devices | to use Bluetooth | Allow |
| Notifications | to show it's running | Allow |
| Battery optimisation | so it keeps counting with the screen off | Allow |

If you denied one by mistake, go to **Settings → Apps → Stardew Steps → Permissions** and turn it on.

On Samsung, Xiaomi, OnePlus and some other phones, also open **Settings → Apps → Stardew Steps → Battery** and choose **Unrestricted**. Otherwise the phone may stop the app in the background.

### Step 8. Choose your PC in the app

Tap **Choose computer** and pick your PC from the list. The app remembers it.

---

## Part 3: Play

Do this each time you play.

1. **Start the bridge** on the PC. Leave it running, or set it to start on its own (see below).
   ```bash
   cd ~/stardew-steps/bridge && python3 -m stardew_steps_bridge
   ```
   It prints `waiting for the phone over Bluetooth`.
2. **Start Stardew Valley** and load your farm.
3. **On the phone, open Stardew Steps and tap Connect.**
   - The app shows **Connected to (your PC)**.
   - The bridge prints `phone connected`.
4. **Hold a direction key and walk.** Every step you take moves the farmer one tile that way.
   - Walking on a treadmill or around the room both work.
   - You can lock the phone and put it in your pocket.
5. When you're done, tap **Disconnect** in the app.

### Start the bridge automatically (optional)

To have the bridge start every time you log in to the PC:

```bash
mkdir -p ~/.config/systemd/user
cp ~/stardew-steps/bridge/stardew-steps-bridge.service ~/.config/systemd/user/
systemctl --user daemon-reload
systemctl --user enable --now stardew-steps-bridge
```

To see what it's doing, run `journalctl --user -u stardew-steps-bridge -f`. To turn it off, run `systemctl --user disable --now stardew-steps-bridge`.

---

## How it behaves

- **One step, one tile.** The farmer walks one tile in the direction of the key you're holding, then stops.
- **No key, no walking.** Steps you take while not holding a key are thrown away. They don't build up for later.
- **Two keys held.** The key you pressed last wins. The farmer never walks diagonally.
- **Walking faster than the farmer.** Up to 5 steps wait in line. A step that's been waiting more than 2 seconds is dropped, so the farmer never runs off after you've stopped.
- **Walls.** A step into a wall or fence is used up, and the farmer stays put.
- **Menus.** While a menu, conversation or cutscene is open, steps don't count. Menus, shops and the Saloon arcade games keep their normal controls.
- **Controllers.** A controller can't walk the farmer on its own either. It still works in menus.
- **Everything else.** Tools, clicking, talking to people and riding work as normal. Only walking is tied to your steps.

### Settings

After the first run, the mod creates `Mods/StardewSteps/config.json`:

| Setting | Default | Meaning |
|---|---|---|
| `QueueCapacity` | 5 | most steps that can wait in line |
| `MaxStepAgeSeconds` | 2.0 | steps waiting longer than this are dropped |
| `Port` | 47821 | local port the bridge connects to (if you change it, run the bridge with `--mod-port <number>`) |

Edit the file while the game is closed.

---

## Troubleshooting

| What you see | What to do |
|---|---|
| No `[Stardew Steps]` lines in the SMAPI console | The mod isn't in the right place. You need `Mods/StardewSteps/manifest.json`, not `Mods/StardewSteps/StardewSteps/…`. |
| The farmer walks normally when you hold a key | The mod isn't loaded. Check the SMAPI console for `Stardew Steps` in the list of loaded mods. |
| `Couldn't listen on port 47821` in the SMAPI console | Something else is using that port. Change `Port` in `config.json`, then start the bridge with `--mod-port <number>`. |
| The bridge says `ModuleNotFoundError: No module named 'dbus'` or `'gi'` | Install the packages from Step 3. |
| The bridge says `org.freedesktop.DBus.Error.AccessDenied` | Your user isn't allowed to register Bluetooth services. Try once with `sudo python3 -m stardew_steps_bridge` to confirm that's the cause, then open an issue. |
| The bridge says `step dropped (game/mod not reachable)` | The game isn't running, or no farm is loaded yet. Load your farm. |
| The app says "Can't reach (PC) — retrying" | Check the bridge is running and the PC's Bluetooth is on. If it still won't connect, remove the pairing on both devices and pair again (Step 5). |
| The app says "This phone has no step detector" | The phone lacks the sensor, so this phone can't be used. |
| Steps count with the screen on but stop when it's off | Turn off battery optimisation and set the app to Unrestricted (Step 7). |
| The bridge stopped after Bluetooth restarted on the PC | This is on purpose, so it can register again. Start it again, or use the automatic start, which restarts it for you. |

---

## Build it yourself (developers)

You need a Mac or Linux machine with:
- the .NET 8 SDK
- Python 3
- JDK 17
- the Android SDK (platform 35)
- Stardew Valley with SMAPI installed, so the mod build can find the game files

```bash
./scripts/package.sh     # builds everything into dist/
```

To run the tests:
- Mod: `dotnet test mod`
- Bridge: `cd bridge && python3 -m unittest`
- Phone app: `cd android && ./gradlew testDebugUnitTest`

**In-game self-test (Debug build only).** From the game folder, run `STARDEW_STEPS_SELFTEST=<save folder name> ./StardewModdingAPI`. It loads that save, walks the farmer with scripted steps, writes `RESULT PASS/FAIL` lines to the SMAPI log, and quits.

The design and build plan are in `docs/superpowers/`.
