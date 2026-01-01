package com.example.tabletdeck.ui.widgets

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.tabletdeck.ConnectionState
import com.example.tabletdeck.I18n
import com.example.tabletdeck.TabletDeckViewModel
import com.example.tabletdeck.TabletDeckUiState

@Composable
fun ConnectionStatusCard(viewModel: TabletDeckViewModel) {
    val ui by viewModel.uiState.collectAsState()
    val canReconnect = !ui.wsUrl.isNullOrBlank()

    val showForgetDialog = remember { mutableStateOf(false) }

    if (showForgetDialog.value) {
        AlertDialog(
            onDismissRequest = { showForgetDialog.value = false },
            title = { Text(I18n.t(ui.lang, "pairing.forgetTitle")) },
            text = { Text(I18n.t(ui.lang, "pairing.forgetConfirm")) },
            confirmButton = {
                Button(onClick = {
                    showForgetDialog.value = false
                    viewModel.forgetPairing()
                }) {
                    Text(I18n.t(ui.lang, "pairing.forget"))
                }
            },
            dismissButton = {
                TextButton(onClick = { showForgetDialog.value = false }) {
                    Text(I18n.t(ui.lang, "common.cancel"))
                }
            },
        )
    }

    Card {
        Column(
            modifier = Modifier.fillMaxWidth().padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Text(I18n.t(ui.lang, "pairing.title"), style = MaterialTheme.typography.titleMedium)
            Text(I18n.t(ui.lang, "pairing.status", formatStatus(ui)))

            if (ui.pcName.isNotBlank()) {
                Text(I18n.t(ui.lang, "pairing.pc", ui.pcName), style = MaterialTheme.typography.bodySmall)
            }

            if (canReconnect) {
                Text(I18n.t(ui.lang, "pairing.addr", (ui.wsUrl ?: "—")), style = MaterialTheme.typography.bodySmall)
            }

            Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                Button(
                    enabled = canReconnect && !ui.isConnected,
                    onClick = { viewModel.connect(ui.wsUrl!!) },
                ) {
                    Text(I18n.t(ui.lang, "pairing.connect"))
                }

                OutlinedButton(
                    enabled = ui.isConnected,
                    onClick = { viewModel.disconnect() },
                ) {
                    Text(I18n.t(ui.lang, "pairing.disconnect"))
                }

                OutlinedButton(
                    enabled = canReconnect,
                    onClick = { showForgetDialog.value = true },
                ) {
                    Text(I18n.t(ui.lang, "pairing.forget"))
                }
            }
        }
    }
}

private fun formatStatus(ui: TabletDeckUiState): String {
    return when (ui.connectionState) {
        ConnectionState.DISCONNECTED -> I18n.t(ui.lang, "status.disconnected")
        ConnectionState.CONNECTING -> I18n.t(ui.lang, "status.connecting")
        ConnectionState.CONNECTED -> I18n.t(ui.lang, "status.connected")
        ConnectionState.ERROR -> I18n.t(ui.lang, "status.error", (ui.connectionError ?: "?"))
    }
}
