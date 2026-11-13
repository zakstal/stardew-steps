package com.stardewsteps

import android.Manifest
import android.annotation.SuppressLint
import android.app.Activity
import android.app.AlertDialog
import android.bluetooth.BluetoothManager
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.PowerManager
import android.provider.Settings
import android.view.Gravity
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView

/** Bare-bones screen: pick the paired computer, connect/disconnect, see status and step count. */
class MainActivity : Activity() {
    private lateinit var deviceText: TextView
    private lateinit var statusText: TextView
    private lateinit var countText: TextView
    private lateinit var toggle: Button
    private val ui = Handler(Looper.getMainLooper())
    private val prefs by lazy { getSharedPreferences("stardew-steps", MODE_PRIVATE) }

    private val refresh = object : Runnable {
        override fun run() {
            statusText.text = StepState.status
            countText.text = "Steps: ${StepState.stepsDetected} detected · ${StepState.stepsSent} sent"
            toggle.text = if (StepState.running) "Disconnect" else "Connect"
            ui.postDelayed(this, 500)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val pad = (24 * resources.displayMetrics.density).toInt()
        deviceText = TextView(this).apply { textSize = 18f }
        statusText = TextView(this).apply { textSize = 16f; setPadding(0, pad / 2, 0, 0) }
        countText = TextView(this).apply { textSize = 28f; setPadding(0, pad, 0, pad) }
        val pick = Button(this).apply { text = "Choose computer"; setOnClickListener { pickDevice() } }
        toggle = Button(this).apply { setOnClickListener { onToggle() } }
        setContentView(LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER_HORIZONTAL
            setPadding(pad, pad * 2, pad, pad)
            addView(deviceText); addView(statusText); addView(countText); addView(pick); addView(toggle)
        })
        showDevice()
        requestNeededPermissions()
    }

    override fun onResume() { super.onResume(); ui.post(refresh) }
    override fun onPause() { super.onPause(); ui.removeCallbacks(refresh) }

    private fun showDevice() {
        deviceText.text = "Computer: " + (prefs.getString("name", null) ?: "not chosen")
    }

    private fun neededPermissions(): List<String> = buildList {
        if (Build.VERSION.SDK_INT >= 29) add(Manifest.permission.ACTIVITY_RECOGNITION)
        if (Build.VERSION.SDK_INT >= 31) add(Manifest.permission.BLUETOOTH_CONNECT)
        if (Build.VERSION.SDK_INT >= 33) add(Manifest.permission.POST_NOTIFICATIONS)
    }.filter { checkSelfPermission(it) != PackageManager.PERMISSION_GRANTED }

    private fun requestNeededPermissions() {
        val missing = neededPermissions()
        if (missing.isNotEmpty()) requestPermissions(missing.toTypedArray(), 1)
        else askBatteryExemption()
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (neededPermissions().isEmpty()) askBatteryExemption()
        else StepState.status = "Needs Bluetooth and physical-activity permission"
    }

    /** Many phones stop step detection with the screen off unless the app is exempt. */
    @SuppressLint("BatteryLife")
    private fun askBatteryExemption() {
        val pm = getSystemService(PowerManager::class.java)
        if (!pm.isIgnoringBatteryOptimizations(packageName)) {
            startActivity(Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS, Uri.parse("package:$packageName")))
        }
    }

    @SuppressLint("MissingPermission")
    private fun pickDevice() {
        if (neededPermissions().isNotEmpty()) { requestNeededPermissions(); return }
        val adapter = getSystemService(BluetoothManager::class.java).adapter
        val devices = adapter?.bondedDevices?.toList().orEmpty()
        if (devices.isEmpty()) {
            AlertDialog.Builder(this).setMessage("No paired devices. Pair your computer in Bluetooth settings first.")
                .setPositiveButton("OK", null).show()
            return
        }
        val names = devices.map { it.name ?: it.address }.toTypedArray()
        AlertDialog.Builder(this).setTitle("Choose computer").setItems(names) { _, i ->
            prefs.edit().putString("address", devices[i].address).putString("name", names[i]).apply()
            showDevice()
        }.show()
    }

    private fun onToggle() {
        if (StepState.running) { stopStepService(); return }
        if (neededPermissions().isNotEmpty()) { requestNeededPermissions(); return }
        val address = prefs.getString("address", null)
        if (address == null) { pickDevice(); return }
        startStepService(address)
    }
}
