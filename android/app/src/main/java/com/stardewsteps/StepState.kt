package com.stardewsteps

/** Shared state the service writes and the screen reads (same process). */
object StepState {
    @Volatile var running = false
    @Volatile var status = "Stopped"
    @Volatile var stepsDetected = 0
    @Volatile var stepsSent = 0
}
