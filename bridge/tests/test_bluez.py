"""bluez.py with stand-in dbus/gi modules (the real ones are Linux-only)."""

import sys
import types
import unittest


def _install_fakes():
    dbus = types.ModuleType("dbus")
    service = types.ModuleType("dbus.service")
    mainloop = types.ModuleType("dbus.mainloop")
    glib_ml = types.ModuleType("dbus.mainloop.glib")

    class Object:
        def __init__(self, bus, path):
            pass

    service.Object = Object
    service.method = lambda *a, **k: (lambda f: f)
    glib_ml.DBusGMainLoop = lambda **k: None

    class DBusException(Exception):
        pass

    class Manager:
        registered = []

        def RegisterProfile(self, *a):
            Manager.registered.append(a[0])

        def UnregisterProfile(self, path):
            pass

    dbus.service = service
    dbus.mainloop = mainloop
    mainloop.glib = glib_ml
    dbus.SystemBus = lambda: types.SimpleNamespace(get_object=lambda *a: object())
    dbus.Interface = lambda obj, name: Manager()
    dbus.Boolean = bool
    dbus.DBusException = DBusException
    dbus.Manager = Manager

    gi = types.ModuleType("gi")
    repo = types.ModuleType("gi.repository")

    class MainLoop:
        current = None

        def __init__(self):
            MainLoop.current = self
            self.quit_called = False

        def run(self):
            # Simulate bluetoothd going away: BlueZ calls Release() on our profile.
            FakeState.profile_release()

        def quit(self):
            self.quit_called = True

    repo.GLib = types.SimpleNamespace(MainLoop=MainLoop)
    gi.repository = repo
    for name, mod in {
        "dbus": dbus, "dbus.service": service, "dbus.mainloop": mainloop,
        "dbus.mainloop.glib": glib_ml, "gi": gi, "gi.repository": repo,
    }.items():
        sys.modules[name] = mod
    return MainLoop


class FakeState:
    profile_release = None


class BluezTests(unittest.TestCase):
    def test_exits_nonzero_when_bluez_releases_profile(self):
        MainLoop = _install_fakes()
        sys.modules.pop("stardew_steps_bridge.bluez", None)
        from stardew_steps_bridge import bluez

        real_profile = bluez._Profile

        def capture(*a, **k):
            p = real_profile(*a, **k)
            FakeState.profile_release = p.Release
            return p

        bluez._Profile = capture
        try:
            with self.assertRaises(SystemExit) as cm:
                bluez.serve(lambda sock: None)
            self.assertEqual(cm.exception.code, 1)
            self.assertTrue(MainLoop.current.quit_called)
        finally:
            bluez._Profile = real_profile
            for name in ["dbus", "dbus.service", "dbus.mainloop", "dbus.mainloop.glib", "gi", "gi.repository",
                         "stardew_steps_bridge.bluez"]:
                sys.modules.pop(name, None)


if __name__ == "__main__":
    unittest.main()
