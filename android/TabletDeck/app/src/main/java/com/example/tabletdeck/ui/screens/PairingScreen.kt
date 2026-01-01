package com.example.tabletdeck.ui.screens

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.tabletdeck.TabletDeckViewModel
import com.example.tabletdeck.ui.widgets.ConnectionStatusCard
import com.example.tabletdeck.ui.widgets.ManualUrlCard
import com.example.tabletdeck.ui.widgets.QrScanCard

@Composable
fun PairingScreen(viewModel: TabletDeckViewModel) {
    val ui by viewModel.uiState.collectAsState()
    Column(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        ConnectionStatusCard(viewModel)

        QrScanCard(lang = ui.lang, onScanned = { url -> viewModel.pair(url) })
        ManualUrlCard(lang = ui.lang, onConnect = { url -> viewModel.pair(url) })
    }
}
