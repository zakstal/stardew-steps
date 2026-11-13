import socket
import threading
import time
import unittest

from stardew_steps_bridge.relay import ModRelay


def free_port():
    s = socket.socket()
    s.bind(("127.0.0.1", 0))
    port = s.getsockname()[1]
    s.close()
    return port


class FakeMod:
    """Minimal TCP server collecting everything clients send."""

    def __init__(self, port=0):
        self.sock = socket.socket()
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.sock.bind(("127.0.0.1", port))
        self.sock.listen()
        self.port = self.sock.getsockname()[1]
        self.data = b""
        threading.Thread(target=self._serve, daemon=True).start()

    def _serve(self):
        try:
            conn, _ = self.sock.accept()
        except OSError:
            return
        while True:
            chunk = conn.recv(1024)
            if not chunk:
                return
            self.data += chunk

    def wait_for(self, expected, timeout=2.0):
        end = time.time() + timeout
        while self.data != expected and time.time() < end:
            time.sleep(0.01)
        return self.data

    def close(self):
        self.sock.close()


class RelayTests(unittest.TestCase):
    def test_relay_sends_step_line(self):
        mod = FakeMod()
        relay = ModRelay(port=mod.port, retry_s=0.1)
        try:
            self.assertTrue(relay.send_step())
            self.assertEqual(mod.wait_for(b"step\n"), b"step\n")
        finally:
            relay.close()
            mod.close()

    def test_relay_drops_steps_while_mod_down_then_recovers(self):
        port = free_port()
        relay = ModRelay(port=port, retry_s=0.1)
        try:
            self.assertFalse(relay.send_step())
            mod = FakeMod(port)
            time.sleep(0.2)
            self.assertTrue(relay.send_step())
            self.assertEqual(mod.wait_for(b"step\n"), b"step\n")
            mod.close()
        finally:
            relay.close()


if __name__ == "__main__":
    unittest.main()
