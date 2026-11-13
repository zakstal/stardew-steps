package com.stardewsteps

import android.annotation.SuppressLint
import android.bluetooth.BluetoothDevice
import android.bluetooth.BluetoothSocket
import java.io.IOException

/** One RFCOMM connection to the bridge. Not reusable: make a new one to reconnect. */
class BtLink(private val device: BluetoothDevice) {
    private var socket: BluetoothSocket? = null

    @SuppressLint("MissingPermission") // checked by MainActivity before the service starts
    @Throws(IOException::class)
    fun connect() {
        val s = device.createRfcommSocketToServiceRecord(StepProtocol.SPP_UUID)
        socket = s
        s.connect()
    }

    @Synchronized
    @Throws(IOException::class)
    fun send(bytes: ByteArray) {
        val out = socket?.outputStream ?: throw IOException("not connected")
        out.write(bytes)
        out.flush()
    }

    fun close() {
        try { socket?.close() } catch (_: IOException) {}
        socket = null
    }
}
