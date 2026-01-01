package com.example.tabletdeck.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.tabletdeck.StopwatchUiState
import com.example.tabletdeck.TabletDeckViewModel
import java.util.concurrent.TimeUnit

@Composable
fun MediaControlsScreen(viewModel: TabletDeckViewModel) {
    val ui by viewModel.uiState.collectAsState()

    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        Text("Sterowanie mediami", style = MaterialTheme.typography.titleLarge)

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("media:playpause") }) { Text("Play/Pause") }
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("media:next") }) { Text("Next") }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("media:stop") }) { Text("Stop") }
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("media:mute") }) { Text("Mute") }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("media:voldown") }) { Text("Vol-") }
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("media:volup") }) { Text("Vol+") }
        }

        Spacer(Modifier.height(12.dp))

        Text("OBS Studio", style = MaterialTheme.typography.titleLarge)

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.toggleObsStream() }) {
                Text(if (ui.obsStreamStopwatch.running) "Stream STOP" else "Stream START")
            }
            OutlinedButton(enabled = ui.isConnected, onClick = { viewModel.resetObsStreamStopwatch() }) { Text("Reset") }
            Text(text = formatStopwatch(ui.obsStreamStopwatch), style = MaterialTheme.typography.titleMedium)
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.toggleObsRecord() }) {
                Text(if (ui.obsRecordStopwatch.running) "Record STOP" else "Record START")
            }
            OutlinedButton(enabled = ui.isConnected, onClick = { viewModel.resetObsRecordStopwatch() }) { Text("Reset") }
            Text(text = formatStopwatch(ui.obsRecordStopwatch), style = MaterialTheme.typography.titleMedium)
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("obs:scene:prev") }) { Text("Scene -") }
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("obs:scene:next") }) { Text("Scene +") }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("obs:audio:mic:toggleMute") }) { Text("Mic Mute") }
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("obs:audio:desktop:toggleMute") }) { Text("Desktop Mute") }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            Button(enabled = ui.isConnected, onClick = { viewModel.sendAction("obs:replay:save") }) { Text("Replay Save") }
        }
    }
}

private fun formatStopwatch(s: StopwatchUiState): String {
    val totalSeconds = TimeUnit.MILLISECONDS.toSeconds(s.elapsedMs)
    val hours = totalSeconds / 3600
    val minutes = (totalSeconds % 3600) / 60
    val seconds = totalSeconds % 60
    return if (hours > 0) "%d:%02d:%02d".format(hours, minutes, seconds) else "%02d:%02d".format(minutes, seconds)
}
