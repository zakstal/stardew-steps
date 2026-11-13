package com.stardewsteps

import java.util.UUID

/** Wire format shared with the Fedora bridge: newline-terminated ASCII lines. */
object StepProtocol {
    val STEP: ByteArray = "step\n".toByteArray(Charsets.US_ASCII)
    val PING: ByteArray = "ping\n".toByteArray(Charsets.US_ASCII)
    const val PING_INTERVAL_MS = 10_000L
    /** Bluetooth Serial Port Profile. */
    val SPP_UUID: UUID = UUID.fromString("00001101-0000-1000-8000-00805F9B34FB")
}
