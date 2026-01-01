package com.example.tabletdeck.ui.screens

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.tabletdeck.TabletDeckViewModel
import com.example.tabletdeck.ui.components.LineChart

@Composable
fun StatsScreen(viewModel: TabletDeckViewModel) {
    val ui by viewModel.uiState.collectAsState()

    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        Text("Statystyki PC", style = MaterialTheme.typography.titleLarge)

        Text("CPU: ${ui.metricsCpuPct?.let { "%.1f".format(it) } ?: "—"}%  |  Temp: ${ui.metricsCpuTempC?.let { "%.1f".format(it) } ?: "N/A"}°C")
        LineChart(
            values = ui.historyCpuPct,
            minY = 0f,
            maxY = 100f,
            modifier = Modifier.fillMaxWidth().height(140.dp)
        )

        Text("GPU: ${ui.metricsGpuPct?.let { "%.1f".format(it) } ?: "N/A"}%  |  Temp: ${ui.metricsGpuTempC?.let { "%.1f".format(it) } ?: "N/A"}°C")
        LineChart(
            values = ui.historyGpuPct,
            minY = 0f,
            maxY = 100f,
            modifier = Modifier.fillMaxWidth().height(140.dp)
        )

        val ramLine = if (ui.metricsRamTotalMb != null && ui.metricsRamUsedMb != null) {
            val used = ui.metricsRamUsedMb!!
            val total = ui.metricsRamTotalMb!!
            "RAM: $used / $total MB"
        } else "RAM: —"

        Text(ramLine)
        LineChart(
            values = ui.historyRamUsedMb,
            minY = 0f,
            maxY = (ui.metricsRamTotalMb?.toFloat() ?: 1f),
            modifier = Modifier.fillMaxWidth().height(140.dp)
        )
    }
}