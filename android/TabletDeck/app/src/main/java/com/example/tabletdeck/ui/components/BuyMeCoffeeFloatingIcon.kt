package com.example.tabletdeck.ui.components

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.input.pointer.consumeAllChanges
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.zIndex
import kotlin.math.roundToInt

private const val BUY_ME_A_COFFEE_URL = "https://buymeacoffee.com/kenay"

@Composable
fun BuyMeCoffeeFloatingIcon(
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
    val density = LocalDensity.current

    BoxWithConstraints(modifier = modifier) {
        val size = 44.dp
        val margin = 12.dp

        val maxXpx = with(density) { (maxWidth - size - margin).toPx() }.coerceAtLeast(0f)
        val maxYpx = with(density) { (maxHeight - size - margin).toPx() }.coerceAtLeast(0f)
        val minXpx = with(density) { margin.toPx() }
        val minYpx = with(density) { margin.toPx() }

        val offsetX = rememberSaveable { mutableStateOf(-1f) }
        val offsetY = rememberSaveable { mutableStateOf(-1f) }

        LaunchedEffect(maxXpx, maxYpx, minXpx, minYpx) {
            if (offsetX.value < 0f || offsetY.value < 0f) {
                offsetX.value = maxXpx
                offsetY.value = maxYpx
            } else {
                offsetX.value = offsetX.value.coerceIn(minXpx, maxXpx)
                offsetY.value = offsetY.value.coerceIn(minYpx, maxYpx)
            }
        }

        val interaction = remember { MutableInteractionSource() }

        Box(
            contentAlignment = Alignment.Center,
            modifier = Modifier
                .zIndex(10f)
                .offset { IntOffset(offsetX.value.roundToInt(), offsetY.value.roundToInt()) }
                .size(size)
                .alpha(0.65f)
                .background(MaterialTheme.colorScheme.primaryContainer, CircleShape)
                .pointerInput(maxXpx, maxYpx, minXpx, minYpx) {
                    detectDragGestures { change, dragAmount ->
                        change.consumeAllChanges()
                        offsetX.value = (offsetX.value + dragAmount.x).coerceIn(minXpx, maxXpx)
                        offsetY.value = (offsetY.value + dragAmount.y).coerceIn(minYpx, maxYpx)
                    }
                }
                .clickable(indication = null, interactionSource = interaction) {
                    val intent = Intent(Intent.ACTION_VIEW, Uri.parse(BUY_ME_A_COFFEE_URL))
                    intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                    context.startActivity(intent)
                },
        ) {
            Text(
                text = "☕",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onPrimaryContainer,
            )
        }
    }
}
