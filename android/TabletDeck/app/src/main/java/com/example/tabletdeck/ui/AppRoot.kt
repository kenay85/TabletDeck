package com.example.tabletdeck.ui

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.pager.HorizontalPager
import androidx.compose.foundation.pager.rememberPagerState
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import com.example.tabletdeck.TabletDeckViewModel
import com.example.tabletdeck.ui.components.BuyMeCoffeeFloatingIcon
import com.example.tabletdeck.ui.screens.MediaControlsScreen
import com.example.tabletdeck.ui.screens.PairingScreen
import com.example.tabletdeck.ui.screens.TilesPagerScreen

@Composable
fun AppRoot(viewModel: TabletDeckViewModel) {
    val horizontal = rememberPagerState(initialPage = 0, pageCount = { 3 })

    Box(modifier = Modifier.fillMaxSize()) {
        HorizontalPager(
            state = horizontal,
            modifier = Modifier.fillMaxSize(),
        ) { page ->
            when (page) {
                0 -> PairingScreen(viewModel = viewModel)
                1 -> TilesPagerScreen(viewModel = viewModel)
                2 -> MediaControlsScreen(viewModel = viewModel)
            }
        }

        BuyMeCoffeeFloatingIcon(modifier = Modifier.fillMaxSize())
    }
}
