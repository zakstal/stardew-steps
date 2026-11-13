package com.stardewsteps

import org.junit.Assert.assertEquals
import org.junit.Test

class StepProtocolTest {
    @Test fun stepLine() = assertEquals("step\n", String(StepProtocol.STEP))
    @Test fun pingLine() = assertEquals("ping\n", String(StepProtocol.PING))
    @Test fun pingInterval() = assertEquals(10_000L, StepProtocol.PING_INTERVAL_MS)
    @Test fun sppUuid() = assertEquals("00001101-0000-1000-8000-00805f9b34fb", StepProtocol.SPP_UUID.toString())
}
