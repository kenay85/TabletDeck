package com.example.tabletdeck

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import com.example.tabletdeck.ui.AppRoot
import com.example.tabletdeck.ui.theme.TabletDeckTheme
import androidx.activity.viewModels


class MainActivity : ComponentActivity() {

    private val viewModel: TabletDeckViewModel by viewModels {
        TabletDeckViewModelFactory(applicationContext)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            TabletDeckTheme {
                Surface(
                    modifier = Modifier.fillMaxSize(),
                    color = MaterialTheme.colorScheme.background
                ) {
                    AppRoot(viewModel)
                }
            }
        }
    }
}