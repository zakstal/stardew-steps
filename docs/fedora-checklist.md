# Fedora + phone verification checklist

Checks that could only be done on the real Fedora PC and phone, not on the build Mac. Run the bridge with `--fake` for 1–9.

1. **Keys alone don't walk.** With the bridge stopped, hold a direction key in game. The farmer must not move.
2. **One step, one tile.** Run `--fake`, hold Right and press Enter once. The farmer walks exactly one tile and stops.
3. **No key, no step.** Press Enter without holding a key. Nothing happens.
4. **Last key wins.** Hold Up, then also hold Left, and press Enter. The farmer goes Left.
5. **Walls.** Hold a direction into a fence and press Enter. The farmer doesn't move, and the next step works normally.
6. **Menus.** Open the inventory and press Enter a few times, then close it. The farmer doesn't move afterwards.
7. **Everything else works.** Tools, clicking, talking to villagers and menus all behave normally.
8. **Controller.** If you have one plugged in, the stick doesn't move the farmer, but it still moves around inside menus such as the inventory and shops.
9. **Arcade.** In the Saloon, Journey of the Prairie King plays normally with the keyboard.
10. **Phone connects.** Stop `--fake`, start the normal bridge and connect from the phone. The bridge logs `phone connected`.
11. **Real walking.** Hold Right and take 10 steps. The farmer moves about 10 tiles.
12. **Screen off.** Lock the phone and walk while holding a key. The farmer still moves.
13. **Bluetooth drop.** Turn the phone's Bluetooth off, then on again. The app reconnects within about 10 seconds.
14. **Bridge restart.** Stop the bridge for about 40 seconds, then start it again. The phone reconnects by itself.

