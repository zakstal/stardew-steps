package com.stardewsteps

import android.annotation.SuppressLint
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.bluetooth.BluetoothManager
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import java.io.IOException
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit

/**
 * Foreground service: listens to the step detector and sends each step to the bridge,
 * reconnecting on its own. Keeps running with the screen off.
 */
class StepService : Service(), SensorEventListener {
    companion object {
        const val EXTRA_ADDRESS = "address"
        private const val CHANNEL = "steps"
        private const val NOTIFICATION_ID = 1
    }

    private val io = Executors.newSingleThreadScheduledExecutor()
    @Volatile private var link: BtLink? = null
    @Volatile private var stopping = false
    private var address: String? = null
    private var backoffMs = 1_000L
    private var wakeLock: PowerManager.WakeLock? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        address = intent?.getStringExtra(EXTRA_ADDRESS) ?: address
        if (address == null) { stopSelf(); return START_NOT_STICKY }
        startInForeground()
        if (!StepState.running) start()
        return START_REDELIVER_INTENT
    }

    private fun start() {
        StepState.running = true
        stopping = false
        val sm = getSystemService(SensorManager::class.java)
        val sensor = sm.getDefaultSensor(Sensor.TYPE_STEP_DETECTOR, true)
            ?: sm.getDefaultSensor(Sensor.TYPE_STEP_DETECTOR)
        if (sensor == null) {
            StepState.status = "This phone has no step detector"
            return
        }
        sm.registerListener(this, sensor, SensorManager.SENSOR_DELAY_FASTEST)
        wakeLock = getSystemService(PowerManager::class.java)
            .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "StardewSteps:steps").apply { acquire() }
        io.execute { connectLoop() }
        io.scheduleWithFixedDelay({ send(StepProtocol.PING, isStep = false) },
            StepProtocol.PING_INTERVAL_MS, StepProtocol.PING_INTERVAL_MS, TimeUnit.MILLISECONDS)
    }

    /** Runs on the io thread. */
    @SuppressLint("MissingPermission")
    private fun connectLoop() {
        if (stopping || link != null) return
        val adapter = getSystemService(BluetoothManager::class.java).adapter
        val device = adapter?.getRemoteDevice(address)
        if (adapter == null || !adapter.isEnabled || device == null) {
            StepState.status = "Bluetooth is off — waiting"
            retryLater(); return
        }
        StepState.status = "Connecting to ${device.name ?: address}…"
        val l = BtLink(device)
        try {
            l.connect()
            link = l
            backoffMs = 1_000L
            StepState.status = "Connected to ${device.name ?: address}"
        } catch (e: IOException) {
            l.close()
            StepState.status = "Can't reach ${device.name ?: address} — retrying"
            retryLater()
        }
    }

    private fun retryLater() {
        if (stopping) return
        io.schedule({ connectLoop() }, backoffMs, TimeUnit.MILLISECONDS)
        backoffMs = (backoffMs * 2).coerceAtMost(10_000L)
    }

    private fun send(bytes: ByteArray, isStep: Boolean) {
        val l = link ?: return
        try {
            l.send(bytes)
            if (isStep) StepState.stepsSent++
        } catch (e: IOException) {
            l.close()
            link = null
            StepState.status = "Connection lost — reconnecting"
            connectLoop()
        }
    }

    override fun onSensorChanged(event: SensorEvent) {
        if (event.sensor.type != Sensor.TYPE_STEP_DETECTOR) return
        StepState.stepsDetected++
        io.execute { send(StepProtocol.STEP, isStep = true) }
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) {}

    override fun onDestroy() {
        stopping = true
        StepState.running = false
        StepState.status = "Stopped"
        getSystemService(SensorManager::class.java).unregisterListener(this)
        io.shutdownNow()
        link?.close()
        link = null
        wakeLock?.let { if (it.isHeld) it.release() }
        super.onDestroy()
    }

    private fun startInForeground() {
        val nm = getSystemService(NotificationManager::class.java)
        nm.createNotificationChannel(NotificationChannel(CHANNEL, "Step sending", NotificationManager.IMPORTANCE_LOW))
        val open = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE)
        val n: Notification = Notification.Builder(this, CHANNEL)
            .setContentTitle("Stardew Steps")
            .setContentText("Sending your steps to Stardew")
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setContentIntent(open)
            .setOngoing(true)
            .build()
        if (Build.VERSION.SDK_INT >= 34) {
            startForeground(NOTIFICATION_ID, n,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH or ServiceInfo.FOREGROUND_SERVICE_TYPE_CONNECTED_DEVICE)
        } else {
            startForeground(NOTIFICATION_ID, n)
        }
    }
}

/** Convenience for starting/stopping from the screen. */
fun Context.startStepService(address: String) =
    startForegroundService(Intent(this, StepService::class.java).putExtra(StepService.EXTRA_ADDRESS, address))

fun Context.stopStepService() = stopService(Intent(this, StepService::class.java))
