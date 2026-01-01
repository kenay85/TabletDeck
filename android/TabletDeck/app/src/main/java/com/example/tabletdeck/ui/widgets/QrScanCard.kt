package com.example.tabletdeck.ui.widgets


import android.Manifest
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import com.example.tabletdeck.I18n

@Composable
fun QrScanCard(lang: String, onScanned: (String) -> Unit) {
    val context = LocalContext.current
    var hasCamera by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED
        )
    }

    val permLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        hasCamera = granted
    }

    val scanLauncher = rememberLauncherForActivityResult(ScanContract()) { result ->
        val contents = result.contents
        if (!contents.isNullOrBlank()) onScanned(contents.trim())
    }

    Card {
        Column(
            Modifier.fillMaxWidth().padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text("Parowanie QR", style = MaterialTheme.typography.titleMedium)

            if (!hasCamera) {
                Text(I18n.t(lang, "pairing.cameraRequired"))
                Button(onClick = { permLauncher.launch(Manifest.permission.CAMERA) }) {
                    Text(I18n.t(lang, "pairing.allowCamera"))
                }
            } else {
                Button(onClick = {
                    val options = ScanOptions()
                        .setPrompt(I18n.t(lang, "pairing.qrPrompt"))
                        .setBeepEnabled(false)
                        .setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                        .setOrientationLocked(true)
                    scanLauncher.launch(options)
                }) {
                    Text(I18n.t(lang, "pairing.qrScan"))
                }
            }
        }
    }
}