import socket
import threading
import time
import unittest

from stardew_steps_bridge.phone import PhoneHub


def wait_until(cond, timeout=2.0):
    end = time.time() + timeout
    while not cond() and time.time() < end:
        time.sleep(0.01)
    return cond()


def reads_eof(sock, timeout=2.0):
    sock.settimeout(timeout)
    try:
        return sock.recv(16) == b""
    except (socket.timeout, ConnectionResetError):
        return False


class PhoneTests(unittest.TestCase):
    def setUp(self):
        self.count = 0
        self.lock = threading.Lock()

    def on_step(self):
        with self.lock:
            self.count += 1

    def test_phone_step_lines_call_on_step(self):
        hub = PhoneHub(self.on_step, idle_timeout_s=5)
        ours, phone = socket.socketpair()
        try:
            hub.attach(ours)
            phone.sendall(b"step\nping\nstep\n")
            self.assertTrue(wait_until(lambda: self.count == 2))
            time.sleep(0.1)
            self.assertEqual(self.count, 2)
        finally:
            hub.close()
            phone.close()

    def test_phone_idle_timeout_closes(self):
        hub = PhoneHub(self.on_step, idle_timeout_s=0.2)
        ours, phone = socket.socketpair()
        try:
            hub.attach(ours)
            self.assertTrue(reads_eof(phone, timeout=1.0))
        finally:
            hub.close()
            phone.close()

    def test_new_phone_connection_replaces_old(self):
        hub = PhoneHub(self.on_step, idle_timeout_s=5)
        a_ours, a_phone = socket.socketpair()
        b_ours, b_phone = socket.socketpair()
        try:
            hub.attach(a_ours)
            hub.attach(b_ours)
            self.assertTrue(reads_eof(a_phone))
            b_phone.sendall(b"step\n")
            self.assertTrue(wait_until(lambda: self.count == 1))
        finally:
            hub.close()
            a_phone.close()
            b_phone.close()


if __name__ == "__main__":
    unittest.main()
