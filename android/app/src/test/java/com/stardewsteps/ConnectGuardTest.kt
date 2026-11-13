package com.stardewsteps

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.Closeable

class ConnectGuardTest {
    private class FakeLink : Closeable {
        var closed = 0
        override fun close() { closed++ }
    }

    @Test fun adoptsWhenRunning() {
        val g = ConnectGuard<FakeLink>()
        val l = FakeLink()
        g.connecting(l)
        assertTrue(g.adopt(l))
        assertSame(l, g.current)
        assertEquals(0, l.closed)
    }

    @Test fun stopDuringConnectClosesPendingAndRejectsAdopt() {
        val g = ConnectGuard<FakeLink>()
        val l = FakeLink()
        g.connecting(l)
        g.stop()                    // user taps Disconnect while connect() blocks
        assertTrue(l.closed >= 1)   // closing aborts the blocking connect
        assertFalse(g.adopt(l))     // connect() returned anyway: don't keep it
        assertNull(g.current)
    }

    @Test fun stopClosesCurrent() {
        val g = ConnectGuard<FakeLink>()
        val l = FakeLink()
        g.connecting(l); g.adopt(l)
        g.stop()
        assertEquals(1, l.closed)
        assertNull(g.current)
        assertTrue(g.stopped)
    }

    @Test fun dropOnlyClearsMatchingLink() {
        val g = ConnectGuard<FakeLink>()
        val a = FakeLink(); val b = FakeLink()
        g.connecting(a); g.adopt(a)
        g.connecting(b); g.adopt(b)
        g.drop(a)
        assertSame(b, g.current)
        assertTrue(a.closed >= 1)
    }
}
