package com.example.tabletdeck.ui.screens

import android.net.Uri
import androidx.activity.compose.LocalActivityResultRegistryOwner
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.example.tabletdeck.TabletDeckViewModel
import com.example.tabletdeck.I18n
import com.example.tabletdeck.ConnectionState

@Composable
fun FileTransferScreen(viewModel: TabletDeckViewModel) {
    val ctx = LocalContext.current
    val ui by viewModel.uiState.collectAsState()

    // CRASH FIX: w subcomposition (Pager/Lazy) czasem nie ma registry owner.
    // Jeśli jest null -> NIE tworzymy launchera (to potrafi crashować).
    val registryOwner = LocalActivityResultRegistryOwner.current
    if (registryOwner == null) {
        Column(
            modifier = Modifier.fillMaxSize().padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(I18n.t(ui.lang, "file.title"), style = MaterialTheme.typography.titleLarge)
            Text("Ten ekran nie ma ActivityResultRegistryOwner (pager/subcomposition).")
            Text("Przesuń ekran i wróć, albo kliknij w inne zakładki.")
        }
        return
    }

    var picked by remember { mutableStateOf<Uri?>(null) }
    var lastMsg by remember { mutableStateOf<String?>(null) }

    val picker = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        picked = uri
        lastMsg = null
    }

    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Text(I18n.t(ui.lang, "file.title"), style = MaterialTheme.typography.titleLarge)
        Text(I18n.t(ui.lang, "pairing.status", formatStatus(ui)))

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(onClick = { picker.launch(arrayOf("*/*")) }) {
                Text(I18n.t(ui.lang, "file.pick"))
            }
            Button(
                enabled = picked != null && ui.isConnected,
                onClick = {
                    val uri = picked ?: return@Button
                    viewModel.uploadFile(ctx, uri) { ok, msg ->
                        lastMsg = if (ok) I18n.t(ui.lang, "file.sent", msg) else I18n.t(ui.lang, "file.err", msg)
                    }
                }
            ) {
                Text(I18n.t(ui.lang, "file.send"))
            }
        }

        Text(I18n.t(ui.lang, "file.chosen", (picked?.toString() ?: "—")))
        if (!lastMsg.isNullOrBlank()) Text(lastMsg!!)
        Text(
            I18n.t(ui.lang, "file.dest"),
            style = MaterialTheme.typography.bodySmall
        )
    }
}

private fun formatStatus(ui: com.example.tabletdeck.TabletDeckUiState): String {
    return when (ui.connectionState) {
        ConnectionState.DISCONNECTED -> I18n.t(ui.lang, "status.disconnected")
        ConnectionState.CONNECTING -> I18n.t(ui.lang, "status.connecting")
        ConnectionState.CONNECTED -> I18n.t(ui.lang, "status.connected")
        ConnectionState.ERROR -> I18n.t(ui.lang, "status.error", (ui.connectionError ?: "?"))
    }
}
