"""Reads step/ping lines from the connected phone. One phone at a time."""

import logging
import socket
import threading

log = logging.getLogger(__name__)


class PhoneHub:
    def __init__(self, on_step, idle_timeout_s=30.0):
        self.on_step = on_step
        self.idle_timeout_s = idle_timeout_s
        self._current = None
        self._lock = threading.Lock()

    def attach(self, sock: socket.socket) -> None:
        """Take over a new phone connection, closing any previous one."""
        with self._lock:
            if self._current is not None:
                log.info("new phone connection replaces the old one")
                _close(self._current)
            self._current = sock
        log.info("phone connected")
        threading.Thread(target=self._read, args=(sock,), daemon=True).start()

    def close(self):
        with self._lock:
            if self._current is not None:
                _close(self._current)
                self._current = None

    def _read(self, sock):
        sock.settimeout(self.idle_timeout_s)
        buf = b""
        try:
            while True:
                chunk = sock.recv(256)
                if not chunk:
                    break
                buf += chunk
                *lines, buf = buf.split(b"\n")
                buf = buf[-256:]
                for line in lines:
                    if line.strip() == b"step":
                        self.on_step()
        except socket.timeout:
            log.info("phone silent for %.0fs; closing", self.idle_timeout_s)
        except OSError:
            pass
        finally:
            _close(sock)
            with self._lock:
                if self._current is sock:
                    self._current = None
                    log.info("phone disconnected")


def _close(sock):
    try:
        sock.shutdown(socket.SHUT_RDWR)
    except OSError:
        pass
    sock.close()
