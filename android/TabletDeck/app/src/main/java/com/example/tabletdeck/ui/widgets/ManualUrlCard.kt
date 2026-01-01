package com.example.tabletdeck.ui.widgets


import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.tabletdeck.I18n

@Composable
fun ManualUrlCard(lang: String, onConnect: (String) -> Unit) {
    var url by remember { mutableStateOf("") }

    Card {
        Column(
            Modifier.fillMaxWidth().padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text("Ręczne wpisanie (awaryjne)", style = MaterialTheme.typography.titleMedium)
            OutlinedTextField(
                value = url,
                onValueChange = { url = it },
                label = { Text(I18n.t(lang, "pairing.manualHint")) },
                modifier = Modifier.fillMaxWidth()
            )
            Button(onClick = { if (url.isNotBlank()) onConnect(url.trim()) }) {
                Text(I18n.t(lang, "pairing.connect"))
            }
        }
    }
}

