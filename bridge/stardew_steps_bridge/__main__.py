"""Stardew Steps bridge: phone (Bluetooth) -> SMAPI mod (TCP localhost)."""

import argparse
import logging
import sys
import time

from .phone import PhoneHub
from .relay import ModRelay

log = logging.getLogger("stardew_steps_bridge")


def main(argv=None):
    p = argparse.ArgumentParser(prog="stardew_steps_bridge", description=__doc__)
    p.add_argument("--mod-port", type=int, default=47821, help="SMAPI mod TCP port (default 47821)")
    fake = p.add_mutually_exclusive_group()
    fake.add_argument("--fake", action="store_true", help="no phone: each Enter key press sends one step")
    fake.add_argument("--fake-rate", type=float, metavar="N", help="no phone: send N steps per second")
    args = p.parse_args(argv)

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s", datefmt="%H:%M:%S")
    relay = ModRelay(port=args.mod_port)

    def step():
        if not relay.send_step():
            log.info("step dropped (game/mod not reachable)")

    try:
        if args.fake:
            print("Fake mode: press Enter to send a step, Ctrl+C to quit.")
            for _ in sys.stdin:
                step()
        elif args.fake_rate:
            print(f"Fake mode: {args.fake_rate} steps/s, Ctrl+C to quit.")
            while True:
                step()
                time.sleep(1.0 / args.fake_rate)
        else:
            from . import bluez  # needs dbus-python + PyGObject (Linux only)

            hub = PhoneHub(step)
            bluez.serve(hub.attach)
    except KeyboardInterrupt:
        pass
    finally:
        relay.close()


if __name__ == "__main__":
    main()
