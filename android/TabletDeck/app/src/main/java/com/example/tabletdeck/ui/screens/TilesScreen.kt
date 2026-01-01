package com.example.tabletdeck.ui.screens

import android.graphics.BitmapFactory
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.example.tabletdeck.TabletDeckViewModel
import java.util.Base64

@Composable
fun TilesScreen(viewModel: TabletDeckViewModel) {
    val ui by viewModel.uiState.collectAsState()

    val rows = ui.layoutRows.coerceIn(1, 12)
    val cols = ui.layoutCols.coerceIn(1, 12)
    val tileHeight = ui.tileHeightDp.coerceIn(48, 400).dp
    val iconSize = ui.iconSizeDp.coerceIn(24, 256).dp

    val cells = remember(ui.layoutCells, rows, cols) {
        val target = rows * cols
        val src = ui.layoutCells
        val out = MutableList<String?>(target) { null }
        for (i in 0 until minOf(target, src.size)) out[i] = src[i]
        out
    }

    Box(modifier = Modifier.fillMaxSize().padding(horizontal = 10.dp, vertical = 10.dp)) {
        LazyVerticalGrid(
            columns = GridCells.Fixed(cols),
            modifier = Modifier.fillMaxSize(),
            verticalArrangement = Arrangement.spacedBy(8.dp),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            contentPadding = PaddingValues(bottom = 6.dp)
        ) {
            itemsIndexed(cells) { _, actionId ->
                val enabled = !actionId.isNullOrBlank()
                val label = actionId?.let { ui.actionLabels[it] }.orEmpty()
                val iconB64 = actionId?.let { ui.actionIconsBase64[it] }.orEmpty()

                MatrixTile(
                    label = if (label.isNotBlank()) label else "—",
                    enabled = enabled,
                    iconBase64 = iconB64,
                    iconSize = iconSize,
                    onClick = { if (enabled) viewModel.sendAction(actionId!!) },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(tileHeight)
                )
            }
        }
    }
}

@Composable
private fun MatrixTile(
    label: String,
    enabled: Boolean,
    iconBase64: String,
    iconSize: Dp,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val shape = RoundedCornerShape(14.dp)

    val iconBitmap = remember(iconBase64) {
        try {
            if (iconBase64.isBlank()) null
            else {
                val bytes = Base64.getDecoder().decode(iconBase64)
                BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
            }
        } catch (_: Exception) {
            null
        }
    }

    Card(
        modifier = modifier,
        shape = shape,
        onClick = { if (enabled) onClick() },
        enabled = enabled,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.55f)),
        colors = CardDefaults.cardColors(
            containerColor = if (enabled)
                MaterialTheme.colorScheme.surface
            else
                MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.55f)
        )
    ) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(horizontal = 10.dp, vertical = 10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                if (iconBitmap != null) {
                    Image(
                        bitmap = iconBitmap.asImageBitmap(),
                        contentDescription = null,
                        modifier = Modifier.size(iconSize)
                    )
                }
            }

            Text(
                text = label,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                textAlign = TextAlign.Center,
                color = MaterialTheme.colorScheme.onSurface,
                style = MaterialTheme.typography.bodySmall
            )
        }
    }
}