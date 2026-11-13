package com.stardewsteps

import java.io.Closeable

/**
 * Tracks the link being connected and the live link, so stopping the service
 * always closes both — even while a blocking connect() is in progress.
 */
class ConnectGuard<T : Closeable> {
    private var pending: T? = null
    @Volatile var current: T? = null
        private set
    @Volatile var stopped = false
        private set

    @Synchronized fun connecting(link: T) {
        if (stopped) { link.close(); return }
        pending = link
    }

    /** Call when connect() returns. False (and the link is closed) if we stopped meanwhile. */
    @Synchronized fun adopt(link: T): Boolean {
        if (pending === link) pending = null
        if (stopped) { link.close(); return false }
        current?.takeIf { it !== link }?.close()
        current = link
        return true
    }

    /** Close a failed link; clears it if it's the live one. */
    @Synchronized fun drop(link: T) {
        link.close()
        if (pending === link) pending = null
        if (current === link) current = null
    }

    @Synchronized fun stop() {
        stopped = true
        pending?.close(); pending = null
        current?.close(); current = null
    }
}
