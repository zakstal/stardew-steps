"""TCP client that forwards steps to the SMAPI mod on localhost."""

import logging
import select
import socket
import threading
import time

log = logging.getLogger(__name__)


class ModRelay:
    def __init__(self, host="127.0.0.1", port=47821, retry_s=3.0):
        self.host = host
        self.port = port
        self.retry_s = retry_s
        self._sock = None
        self._last_attempt = float("-inf")
        self._lock = threading.Lock()

    def send_step(self) -> bool:
        """Send one step. Returns False (step dropped) if the mod isn't reachable. Never raises."""
        with self._lock:
            if self._sock is not None and self._peer_closed():
                self._drop("mod closed the connection")
            if self._sock is None and not self._try_connect():
                return False
            try:
                self._sock.sendall(b"step\n")
                return True
            except OSError as e:
                self._drop(f"send failed: {e}")
                return False

    def close(self):
        with self._lock:
            if self._sock is not None:
                self._sock.close()
                self._sock = None

    def _try_connect(self) -> bool:
        now = time.monotonic()
        if now - self._last_attempt < self.retry_s:
            return False
        self._last_attempt = now
        try:
            self._sock = socket.create_connection((self.host, self.port), timeout=1.0)
            log.info("connected to mod on %s:%d", self.host, self.port)
            return True
        except OSError:
            self._sock = None
            return False

    def _peer_closed(self) -> bool:
        try:
            readable, _, _ = select.select([self._sock], [], [], 0)
            return bool(readable) and self._sock.recv(16) == b""
        except OSError:
            return True

    def _drop(self, reason):
        log.info("lost mod connection (%s); steps dropped until it's back", reason)
        try:
            self._sock.close()
        except OSError:
            pass
        self._sock = None
        self._last_attempt = float("-inf")
