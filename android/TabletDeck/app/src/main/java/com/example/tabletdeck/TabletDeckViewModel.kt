package com.example.tabletdeck

import android.content.Context
import android.net.Uri
import android.os.SystemClock
import android.util.Base64
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import okhttp3.MediaType
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit
import okhttp3.Request
import okhttp3.RequestBody
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okio.BufferedSink
import org.json.JSONObject
import java.io.InputStream
import java.net.URI
import android.content.ContentValues
import android.os.Build
import android.os.Environment
import android.os.Looper
import android.provider.MediaStore
import android.widget.Toast
import java.io.File
import java.io.FileOutputStream
import java.io.OutputStream
import java.util.concurrent.ConcurrentHashMap

enum class ConnectionState { DISCONNECTED, CONNECTING, CONNECTED, ERROR }

data class StopwatchUiState(
    val running: Boolean = false,
    val elapsedMs: Long = 0L,
)

data class TabletDeckUiState(
    val lang: String = "en",
    val connectionState: ConnectionState = ConnectionState.DISCONNECTED,
    val connectionError: String? = null,
    val isConnected: Boolean = false,
    val pcName: String = "",
    val wsUrl: String? = null,

    val actionLabels: Map<String, String> = emptyMap(),
    val actionIconsBase64: Map<String, String> = emptyMap(),
    val layoutCells: List<String?> = emptyList(),

    /** Dynamiczna siatka (live z Windows). */
    val layoutRows: Int = 4,
    val layoutCols: Int = 5,
    val tileHeightDp: Int = 126,
    val iconSizeDp: Int = 82,

    val metricsCpuPct: Float? = null,
    val metricsCpuTempC: Float? = null,
    val metricsGpuPct: Float? = null,
    val metricsGpuTempC: Float? = null,
    val metricsRamUsedMb: Int? = null,
    val metricsRamTotalMb: Int? = null,

    /** Wykresy. */
    val historyCpuPct: List<Float> = emptyList(),
    val historyGpuPct: List<Float> = emptyList(),
    val historyRamUsedMb: List<Float> = emptyList(),

    /** OBS stopery. */
    val obsStreamStopwatch: StopwatchUiState = StopwatchUiState(),
    val obsRecordStopwatch: StopwatchUiState = StopwatchUiState(),
)

class TabletDeckViewModel(
    private val appContext: Context,
    private val pairingStore: PairingStore
) : ViewModel() {

    private val okHttp = OkHttpClient.Builder()
        .pingInterval(20, TimeUnit.SECONDS)
        .retryOnConnectionFailure(true)
        .build()

    private val _uiState = MutableStateFlow(TabletDeckUiState())
    val uiState: StateFlow<TabletDeckUiState> = _uiState.asStateFlow()

    private var ws: WebSocket? = null
private data class IncomingFileSession(
    val id: String,
    val fileName: String,
    val locationLabel: String,
    val uri: Uri? = null,
    val file: File? = null,
    val out: OutputStream,
    var expectedSeq: Int = 0,
    var receivedBytes: Long = 0L,
    val isPendingMediaStore: Boolean = false
)

private val incomingFiles = ConcurrentHashMap<String, IncomingFileSession>()

private fun showToast(message: String) {
    android.os.Handler(Looper.getMainLooper()).post {
        Toast.makeText(appContext, message, Toast.LENGTH_LONG).show()
    }
}

private fun safeFileName(raw: String): String {
    val trimmed = raw.trim().ifEmpty { "file.bin" }
    return trimmed.replace(Regex("[\\\\/:*?\"<>|]"), "_")
}

private fun openDownloadOutputStream(fileNameRaw: String): IncomingFileSession {
    val fileName = safeFileName(fileNameRaw)

    return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
        val values = ContentValues().apply {
            put(MediaStore.Downloads.DISPLAY_NAME, fileName)
            put(MediaStore.Downloads.MIME_TYPE, "application/octet-stream")
            put(MediaStore.Downloads.RELATIVE_PATH, Environment.DIRECTORY_DOWNLOADS)
            put(MediaStore.Downloads.IS_PENDING, 1)
        }
        val uri = appContext.contentResolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
            ?: throw IllegalStateException("Cannot create MediaStore download")
        val out = appContext.contentResolver.openOutputStream(uri)
            ?: throw IllegalStateException("Cannot open output stream")
        IncomingFileSession(
            id = "",
            fileName = fileName,
            locationLabel = "Downloads/$fileName",
            uri = uri,
            out = out,
            isPendingMediaStore = true
        )
    } else {
        val publicDir = try {
            Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS).apply { mkdirs() }
        } catch (_: Throwable) {
            null
        }

        val targetFile = try {
            publicDir?.let { File(it, fileName) }
        } catch (_: Throwable) {
            null
        }

        val finalFile = targetFile ?: run {
            val appDir = (appContext.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: appContext.filesDir).apply { mkdirs() }
            File(appDir, fileName)
        }

        val out = FileOutputStream(finalFile, false)
        IncomingFileSession(
            id = "",
            fileName = fileName,
            locationLabel = finalFile.absolutePath,
            file = finalFile,
            out = out,
            isPendingMediaStore = false
        )
    }
}

private fun handleFileStart(obj: JSONObject) {
    val id = obj.optString("id", "").trim()
    if (id.isEmpty()) return

    incomingFiles.remove(id)?.let { s ->
        try { s.out.close() } catch (_: Throwable) {}
    }

    try {
        val name = obj.optString("name", "file.bin")
        val session = openDownloadOutputStream(name).copy(id = id)
        incomingFiles[id] = session
        showToast("Odbieranie pliku: ${session.fileName}")
    } catch (t: Throwable) {
        showToast("Błąd zapisu pliku: ${t.message ?: t::class.java.simpleName}")
    }
}

private fun abortSession(id: String, reason: String) {
    val s = incomingFiles.remove(id) ?: return
    try { s.out.close() } catch (_: Throwable) {}
    try {
        if (s.uri != null) {
            appContext.contentResolver.delete(s.uri, null, null)
        } else if (s.file != null) {
            s.file.delete()
        }
    } catch (_: Throwable) {}
    showToast("Przerwano odbiór (${s.fileName}): $reason")
}

private fun handleFileChunk(obj: JSONObject) {
    val id = obj.optString("id", "").trim()
    if (id.isEmpty()) return
    val s = incomingFiles[id] ?: return

    val seq = obj.optInt("seq", -1)
    if (seq != s.expectedSeq) {
        abortSession(id, "niepoprawna kolejność danych")
        return
    }

    val b64 = obj.optString("data", "")
    if (b64.isEmpty()) return

    try {
        val bytes = Base64.decode(b64, Base64.DEFAULT)
        s.out.write(bytes)
        s.receivedBytes += bytes.size.toLong()
        s.expectedSeq += 1
    } catch (t: Throwable) {
        abortSession(id, t.message ?: "błąd zapisu")
    }
}

private fun handleFileEnd(obj: JSONObject) {
    val id = obj.optString("id", "").trim()
    if (id.isEmpty()) return
    val s = incomingFiles.remove(id) ?: return

    try { s.out.flush() } catch (_: Throwable) {}
    try { s.out.close() } catch (_: Throwable) {}

    if (s.isPendingMediaStore && s.uri != null && Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
        try {
            val values = ContentValues().apply { put(MediaStore.Downloads.IS_PENDING, 0) }
            appContext.contentResolver.update(s.uri, values, null, null)
        } catch (_: Throwable) { }
    }

    showToast("Zapisano: ${s.locationLabel}")
}


    private var disconnectRequested: Boolean = false
    private var reconnectJob: Job? = null
    private var reconnectAttempt: Int = 0

    private val cpuHist = ArrayDeque<Float>()
    private val gpuHist = ArrayDeque<Float>()
    private val ramHist = ArrayDeque<Float>()
    private val histMax = 120

    private var obsStreamStartMs: Long? = null
    private var obsStreamAccumMs: Long = 0L
    private var obsRecordStartMs: Long? = null
    private var obsRecordAccumMs: Long = 0L
    private var obsStopwatchJob: Job? = null

    init {
        viewModelScope.launch {
            pairingStore.wsUrlFlow
                .distinctUntilChanged()
                .collect { saved ->
                    if (saved.isNullOrBlank()) return@collect

                    _uiState.update { it.copy(wsUrl = saved) }

                    val alreadyConnectedToSame =
                        _uiState.value.isConnected && _uiState.value.wsUrl == saved

                    if (!alreadyConnectedToSame) {
                        connect(saved)
                    }
                }
        }
    }

    /**
     * Nakłada layout z Windows:
     * - ustawia rows/cols/tileHeightDp/iconSizeDp
     * - normalizuje cells do rows*cols
     */
    private fun applyLayoutFrom(layoutObj: JSONObject?) {
        if (layoutObj == null) return

        val rows = layoutObj.optInt("rows", _uiState.value.layoutRows).coerceIn(1, 12)
        val cols = layoutObj.optInt("cols", _uiState.value.layoutCols).coerceIn(1, 12)
        val tileHeightDp = layoutObj.optInt("tileHeightDp", _uiState.value.tileHeightDp).coerceIn(48, 400)
        val iconSizeDp = layoutObj.optInt("iconSizeDp", _uiState.value.iconSizeDp).coerceIn(24, 256)

        val targetCount = rows * cols
        val out = MutableList<String?>(targetCount) { null }

        val cellsArr = layoutObj.optJSONArray("cells")
        if (cellsArr != null) {
            val n = minOf(targetCount, cellsArr.length())
            for (i in 0 until n) {
                out[i] = if (cellsArr.isNull(i)) null else cellsArr.optString(i, null)
            }
        }

        _uiState.update {
            it.copy(
                layoutRows = rows,
                layoutCols = cols,
                tileHeightDp = tileHeightDp,
                iconSizeDp = iconSizeDp,
                layoutCells = out
            )
        }
    }

    /** Zapisuje wsUrl do pamięci i łączy. */
    fun pair(wsUrl: String) {
        val trimmed = wsUrl.trim()
        if (trimmed.isBlank()) return

        viewModelScope.launch {
            pairingStore.saveWsUrl(trimmed)
        }
        connect(trimmed)
    }

    /** Czyści zapamiętane parowanie. */
    fun forgetPairing() {
        disconnect()

        viewModelScope.launch {
            pairingStore.clear()
        }

        _uiState.update {
            it.copy(
                wsUrl = null,
                pcName = "",
                isConnected = false,
                connectionError = null,
                connectionState = ConnectionState.DISCONNECTED,
            )
        }
    }

    fun connect(wsUrl: String) {
        connectInternal(wsUrl, resetBackoff = true)
    }

    private fun connectInternal(wsUrl: String, resetBackoff: Boolean) {
        disconnect(userInitiated = false)

        disconnectRequested = false
        reconnectJob?.cancel()
        reconnectJob = null
        if (resetBackoff) reconnectAttempt = 0

        _uiState.update { it.copy(connectionState = ConnectionState.CONNECTING, connectionError = null, wsUrl = wsUrl) }

        val req = Request.Builder().url(wsUrl).build()
        ws = okHttp.newWebSocket(req, object : WebSocketListener() {

            override fun onOpen(webSocket: WebSocket, response: Response) {
                _uiState.update { it.copy(connectionState = ConnectionState.CONNECTED, connectionError = null, isConnected = true) }
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                try {
                    val obj = JSONObject(text)
                    when (obj.optString("type")) {

                        "hello" -> {
                            val pcName = obj.optString("pcName", "")
                            val langFromPc = obj.optString("lang", "")
                            val actionsArr = obj.optJSONArray("actions")

                            val labels = mutableMapOf<String, String>()
                            val icons = mutableMapOf<String, String>() // actionId -> base64 png

                            if (actionsArr != null) {
                                for (i in 0 until actionsArr.length()) {
                                    val a = actionsArr.getJSONObject(i)
                                    val id = a.getString("id")
                                    labels[id] = a.optString("label", "")
                                    val iconB64 = a.optString("iconPng", "")
                                        .ifBlank { a.optString("icon", "") }
                                        .trim()
                                    if (iconB64.isNotBlank()) icons[id] = iconB64
                                }
                            }

                            _uiState.update {
                                it.copy(
                                    lang = I18n.normalize(langFromPc),
                                    pcName = pcName,
                                    actionLabels = labels,
                                    actionIconsBase64 = icons
                                )
                            }

                            // ✅ KLUCZ: przywraca dynamiczne rows/cols/sizes + cells
                            applyLayoutFrom(obj.optJSONObject("layout"))
                        }

                        // ✅ LIVE update z Windows (po zmianach w edytorze kafli)
                        "layout" -> {
                            applyLayoutFrom(obj.optJSONObject("layout"))
                        }

                        "lang" -> {
                            val langFromPc = obj.optString("lang", "")
                            _uiState.update { it.copy(lang = I18n.normalize(langFromPc)) }
                        }

                        "file_start" -> {
                            handleFileStart(obj)
                        }

                        "file_chunk" -> {
                            handleFileChunk(obj)
                        }

                        "file_end" -> {
                            handleFileEnd(obj)
                        }

                        "metrics" -> {
                            val cpu = obj.optDouble("cpuPct", Double.NaN).takeIf { !it.isNaN() }?.toFloat()
                            val ramUsed = obj.optInt("ramUsedMb", -1).takeIf { it >= 0 }
                            val ramTotal = obj.optInt("ramTotalMb", -1).takeIf { it >= 0 }

                            val cpuTemp = obj.optDouble("cpuTempC", Double.NaN).takeIf { !it.isNaN() }?.toFloat()
                            val gpuPct = obj.optDouble("gpuPct", Double.NaN).takeIf { !it.isNaN() }?.toFloat()
                            val gpuTemp = obj.optDouble("gpuTempC", Double.NaN).takeIf { !it.isNaN() }?.toFloat()

                            fun push(q: ArrayDeque<Float>, v: Float) {
                                q.addLast(v)
                                while (q.size > histMax) q.removeFirst()
                            }

                            if (cpu != null) push(cpuHist, cpu)
                            if (gpuPct != null) push(gpuHist, gpuPct)
                            if (ramUsed != null) push(ramHist, ramUsed.toFloat())

                            _uiState.update {
                                it.copy(
                                    metricsCpuPct = cpu,
                                    metricsCpuTempC = cpuTemp,
                                    metricsGpuPct = gpuPct,
                                    metricsGpuTempC = gpuTemp,
                                    metricsRamUsedMb = ramUsed,
                                    metricsRamTotalMb = ramTotal,
                                    historyCpuPct = cpuHist.toList(),
                                    historyGpuPct = gpuHist.toList(),
                                    historyRamUsedMb = ramHist.toList()
                                )
                            }
                        }
                    }
                } catch (_: Exception) {
                    // ignore
                }
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                _uiState.update { it.copy(connectionState = ConnectionState.DISCONNECTED, connectionError = null, isConnected = false) }
                if (!disconnectRequested) scheduleReconnect("closed: $code")
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                _uiState.update { it.copy(connectionState = ConnectionState.ERROR, connectionError = (t.message ?: "?"), isConnected = false) }
                if (!disconnectRequested) scheduleReconnect(t.message ?: "failure")
            }
        })
    }

    fun disconnect(userInitiated: Boolean = true) {
        if (userInitiated) {
            disconnectRequested = true
            reconnectJob?.cancel()
            reconnectJob = null
            reconnectAttempt = 0
        }

        try {
            ws?.close(1000, "bye")
        } catch (_: Throwable) {
            // ignore
        }

        ws = null

        _uiState.update {
            it.copy(
                connectionState = ConnectionState.DISCONNECTED,
                connectionError = null,
                isConnected = false,
            )
        }
    }

    private fun scheduleReconnect(reason: String?) {
        val url = _uiState.value.wsUrl ?: return
        if (disconnectRequested) return
        if (reconnectJob?.isActive == true) return

        reconnectJob = viewModelScope.launch {
            while (!disconnectRequested && !_uiState.value.isConnected && _uiState.value.wsUrl == url) {
                val attempt = reconnectAttempt.coerceIn(0, 6)
                val delayMs = (1000L shl attempt).coerceAtMost(30_000L)

                _uiState.update {
                    it.copy(
                        connectionState = ConnectionState.CONNECTING,
                        connectionError = reason,
                        isConnected = false,
                    )
                }

                delay(delayMs)

                if (disconnectRequested || _uiState.value.isConnected || _uiState.value.wsUrl != url) return@launch

                reconnectAttempt = (reconnectAttempt + 1).coerceAtMost(10)
                connectInternal(url, resetBackoff = false)
            }
        }
    }

    fun sendAction(actionId: String) {
        val msg = """{"type":"action","actionId":${JSONObject.quote(actionId)}}"""
        ws?.send(msg)
    }

    fun toggleObsStream() {
        sendAction("obs:stream:toggle")
        toggleStopwatch(isStream = true)
    }

    fun toggleObsRecord() {
        sendAction("obs:record:toggle")
        toggleStopwatch(isStream = false)
    }

    fun resetObsStreamStopwatch() {
        resetStopwatch(isStream = true)
    }

    fun resetObsRecordStopwatch() {
        resetStopwatch(isStream = false)
    }

    private fun toggleStopwatch(isStream: Boolean) {
        val now = SystemClock.elapsedRealtime()
        if (isStream) {
            if (obsStreamStartMs == null) obsStreamStartMs = now else {
                obsStreamAccumMs += now - obsStreamStartMs!!
                obsStreamStartMs = null
            }
        } else {
            if (obsRecordStartMs == null) obsRecordStartMs = now else {
                obsRecordAccumMs += now - obsRecordStartMs!!
                obsRecordStartMs = null
            }
        }
        updateObsStopwatches()
        ensureStopwatchJob()
    }

    private fun resetStopwatch(isStream: Boolean) {
        val now = SystemClock.elapsedRealtime()
        if (isStream) {
            obsStreamAccumMs = 0L
            if (obsStreamStartMs != null) obsStreamStartMs = now
        } else {
            obsRecordAccumMs = 0L
            if (obsRecordStartMs != null) obsRecordStartMs = now
        }
        updateObsStopwatches()
    }

    private fun ensureStopwatchJob() {
        val anyRunning = obsStreamStartMs != null || obsRecordStartMs != null
        if (!anyRunning) {
            obsStopwatchJob?.cancel()
            obsStopwatchJob = null
            return
        }
        if (obsStopwatchJob != null) return

        obsStopwatchJob = viewModelScope.launch {
            while (true) {
                delay(250)
                updateObsStopwatches()
                if (obsStreamStartMs == null && obsRecordStartMs == null) {
                    obsStopwatchJob = null
                    return@launch
                }
            }
        }
    }

    private fun updateObsStopwatches() {
        val now = SystemClock.elapsedRealtime()

        val streamElapsed = obsStreamAccumMs + (obsStreamStartMs?.let { now - it } ?: 0L)
        val recordElapsed = obsRecordAccumMs + (obsRecordStartMs?.let { now - it } ?: 0L)

        _uiState.update {
            it.copy(
                obsStreamStopwatch = StopwatchUiState(
                    running = obsStreamStartMs != null,
                    elapsedMs = streamElapsed
                ),
                obsRecordStopwatch = StopwatchUiState(
                    running = obsRecordStartMs != null,
                    elapsedMs = recordElapsed
                )
            )
        }
    }

    fun uploadFile(context: Context, uri: Uri, cb: (ok: Boolean, msg: String) -> Unit) {
        val wsUrl = _uiState.value.wsUrl ?: run {
            cb(false, "Brak wsUrl")
            return
        }

        val http = deriveHttpUploadUrl(wsUrl) ?: run {
            cb(false, "Nie umiem zbudować URL upload")
            return
        }

        viewModelScope.launch(Dispatchers.IO) {
            try {
                val name = queryDisplayName(context, uri) ?: "upload.bin"
                val body = ContentUriRequestBody(context, uri)

                val req = Request.Builder()
                    .url(http)
                    .put(body)
                    .addHeader("X-Filename", name)
                    .build()

                okHttp.newCall(req).execute().use { resp ->
                    if (!resp.isSuccessful) cb(false, "HTTP ${resp.code}")
                    else cb(true, name)
                }
            } catch (e: Exception) {
                cb(false, e.message ?: "error")
            }
        }
    }

    private fun deriveHttpUploadUrl(wsUrl: String): String? {
        val uri = URI(wsUrl)
        val token = uri.query?.split("&")?.firstOrNull { it.startsWith("token=") } ?: return null
        val scheme = "http"
        val hostPort = "${uri.host}:${uri.port}"
        return "$scheme://$hostPort/upload?$token"
    }

    private fun queryDisplayName(context: Context, uri: Uri): String? {
        val cr = context.contentResolver
        val c = cr.query(uri, arrayOf(android.provider.OpenableColumns.DISPLAY_NAME), null, null, null) ?: return null
        c.use {
            if (!it.moveToFirst()) return null
            val idx = it.getColumnIndex(android.provider.OpenableColumns.DISPLAY_NAME)
            if (idx < 0) return null
            return it.getString(idx)
        }
    }

    companion object {
        fun decodeBase64PngToBytes(b64: String): ByteArray? {
            return try {
                val cleaned = b64.substringAfter("base64,", b64).trim()
                Base64.decode(cleaned, Base64.DEFAULT)
            } catch (_: Exception) {
                null
            }
        }
    }
}

private class ContentUriRequestBody(
    private val context: Context,
    private val uri: Uri
) : RequestBody() {

    override fun contentType(): MediaType? = "application/octet-stream".toMediaTypeOrNull()

    override fun writeTo(sink: BufferedSink) {
        val input: InputStream = context.contentResolver.openInputStream(uri)
            ?: throw IllegalStateException("Cannot open uri")
        input.use {
            val buf = ByteArray(DEFAULT_BUFFER_SIZE)
            while (true) {
                val n = it.read(buf)
                if (n <= 0) break
                sink.write(buf, 0, n)
            }
        }
    }
}