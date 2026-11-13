"""Registers a Bluetooth Serial Port Profile with BlueZ and hands each phone connection over.

Linux only. Needs dbus-python and PyGObject (Fedora: python3-dbus, python3-gobject).
"""

import logging
import socket
import sys

import dbus
import dbus.mainloop.glib
import dbus.service
from gi.repository import GLib

log = logging.getLogger(__name__)

SPP_UUID = "00001101-0000-1000-8000-00805F9B34FB"
PROFILE_PATH = "/stardewsteps/spp"


class _Profile(dbus.service.Object):
    def __init__(self, bus, on_connection, on_release):
        super().__init__(bus, PROFILE_PATH)
        self._on_connection = on_connection
        self._on_release = on_release

    @dbus.service.method("org.bluez.Profile1", in_signature="", out_signature="")
    def Release(self):
        log.info("BlueZ released the profile (bluetoothd stopped or restarted)")
        self._on_release()

    @dbus.service.method("org.bluez.Profile1", in_signature="oha{sv}", out_signature="")
    def NewConnection(self, device, fd, properties):
        sock = socket.socket(fileno=fd.take())
        sock.setblocking(True)
        log.info("Bluetooth connection from %s", device)
        self._on_connection(sock)

    @dbus.service.method("org.bluez.Profile1", in_signature="o", out_signature="")
    def RequestDisconnection(self, device):
        log.info("BlueZ asked to disconnect %s", device)

    @dbus.service.method("org.bluez.Profile1", in_signature="", out_signature="")
    def Cancel(self):
        pass


def serve(on_connection):
    """Register the SPP profile and run forever, calling on_connection(sock) for each phone."""
    dbus.mainloop.glib.DBusGMainLoop(set_as_default=True)
    bus = dbus.SystemBus()
    loop = GLib.MainLoop()
    released = []

    def on_release():
        released.append(True)
        loop.quit()

    profile = _Profile(bus, on_connection, on_release)  # keep a reference so it stays exported
    manager = dbus.Interface(bus.get_object("org.bluez", "/org/bluez"), "org.bluez.ProfileManager1")
    manager.RegisterProfile(
        PROFILE_PATH,
        SPP_UUID,
        {
            "Name": "Stardew Steps",
            "Role": "server",
            "RequireAuthentication": dbus.Boolean(False),
            "RequireAuthorization": dbus.Boolean(False),
        },
    )
    log.info("waiting for the phone over Bluetooth (Serial Port Profile)")
    try:
        loop.run()
    finally:
        try:
            manager.UnregisterProfile(PROFILE_PATH)
        except dbus.DBusException:
            pass
        del profile
    if released:
        # Exit non-zero so systemd (Restart=on-failure) starts us again and we re-register.
        sys.exit(1)
