package com.example.tabletdeck.ui.components

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import kotlin.math.max
import kotlin.math.min

@Composable
fun LineChart(
    values: List<Float>,
    modifier: Modifier = Modifier,
    minY: Float? = null,
    maxY: Float? = null,
    showWhenEmptyText: Boolean = true,
    scanlines: Boolean = true,
    showLastPoint: Boolean = true,
) {
    val primary = MaterialTheme.colorScheme.primary
    val grid = MaterialTheme.colorScheme.outlineVariant
    val glow = MaterialTheme.colorScheme.primary.copy(alpha = 0.22f)
    val textColor = MaterialTheme.colorScheme.onBackground
    val bg = MaterialTheme.colorScheme.background

    if (values.size < 2) {
        if (!showWhenEmptyText) return
        Box(modifier = modifier, contentAlignment = Alignment.Center) {
            Text(
                text = "Brak danych",
                style = MaterialTheme.typography.bodySmall,
                color = textColor
            )
        }
        return
    }

    Canvas(modifier = modifier) {
        val w = size.width
        val h = size.height
        if (w <= 1f || h <= 1f) return@Canvas

        // tło
        drawRect(color = bg)

        // scanlines (matrix vibe)
        if (scanlines) {
            val step = 10f
            var y = 0f
            while (y <= h) {
                drawLine(
                    color = grid.copy(alpha = 0.12f),
                    start = Offset(0f, y),
                    end = Offset(w, y),
                    strokeWidth = 1f
                )
                y += step
            }
        }

        // siatka (4 linie)
        val gridLines = 4
        for (i in 1 until gridLines) {
            val y = h * i / gridLines
            drawLine(
                color = grid.copy(alpha = 0.35f),
                start = Offset(0f, y),
                end = Offset(w, y),
                strokeWidth = 1f
            )
        }

        val vMin = minY ?: (values.minOrNull() ?: 0f)
        val vMax = maxY ?: (values.maxOrNull() ?: 1f)
        val yMinSafe = min(vMin, vMax)
        val yMaxSafe = max(vMin, vMax)
        val range = (yMaxSafe - yMinSafe).let { if (it <= 0.0001f) 1f else it }

        fun yOf(v: Float): Float {
            val t = (v - yMinSafe) / range
            return h - (t * h)
        }

        val n = values.size
        val dx = w / (n - 1).toFloat()

        val path = Path().apply {
            moveTo(0f, yOf(values[0]))
            for (i in 1 until n) {
                lineTo(i * dx, yOf(values[i]))
            }
        }

        // glow (kilka warstw)
        val baseWidth = 4f
        drawPath(
            path = path,
            color = glow.copy(alpha = 0.10f),
            style = Stroke(width = baseWidth + 14f, cap = StrokeCap.Round, join = StrokeJoin.Round)
        )
        drawPath(
            path = path,
            color = glow.copy(alpha = 0.16f),
            style = Stroke(width = baseWidth + 8f, cap = StrokeCap.Round, join = StrokeJoin.Round)
        )
        drawPath(
            path = path,
            color = glow.copy(alpha = 0.22f),
            style = Stroke(width = baseWidth + 4f, cap = StrokeCap.Round, join = StrokeJoin.Round)
        )

        // główna linia
        drawPath(
            path = path,
            color = primary,
            style = Stroke(width = baseWidth, cap = StrokeCap.Round, join = StrokeJoin.Round)
        )

        // punkt końcowy (z poświatą)
        if (showLastPoint) {
            val lastX = (n - 1) * dx
            val lastY = yOf(values.last())
            drawCircle(color = glow.copy(alpha = 0.18f), radius = 16f, center = Offset(lastX, lastY))
            drawCircle(color = glow.copy(alpha = 0.28f), radius = 10f, center = Offset(lastX, lastY))
            drawCircle(color = primary, radius = 6f, center = Offset(lastX, lastY))
        }
    }
}