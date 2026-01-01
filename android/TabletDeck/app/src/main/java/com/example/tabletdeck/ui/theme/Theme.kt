package com.example.tabletdeck.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable

private val MatrixColorScheme = darkColorScheme(
    primary = MatrixGreen,
    onPrimary = MatrixBlack,

    background = MatrixBlack,
    onBackground = MatrixGreen,

    surface = MatrixDarkSurface,
    onSurface = MatrixGreen,

    surfaceVariant = MatrixSurfaceVariant,
    onSurfaceVariant = MatrixGreen,

    outline = MatrixOutline,
    outlineVariant = MatrixGreenDim,
)

@Composable
fun TabletDeckTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = MatrixColorScheme,
        content = content
    )
}