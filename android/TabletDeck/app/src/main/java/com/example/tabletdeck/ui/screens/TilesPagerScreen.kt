package com.example.tabletdeck.ui.screens

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.pager.VerticalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import com.example.tabletdeck.TabletDeckViewModel

@Composable
fun TilesPagerScreen(viewModel: TabletDeckViewModel) {
    val vertical = rememberPagerState(initialPage = 1, pageCount = { 3 })

    VerticalPager(
        state = vertical,
        modifier = Modifier.fillMaxSize(),
    ) { page ->
        when (page) {
            0 -> FileTransferScreen(viewModel = viewModel)
            1 -> TilesScreen(viewModel = viewModel)
            2 -> StatsScreen(viewModel = viewModel)
        }
    }
}