package com.example.tabletdeck

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.dataStore by preferencesDataStore(name = "pairing_store")

class PairingStore(private val context: Context) {
    private val keyWsUrl = stringPreferencesKey("ws_url")

    val wsUrlFlow: Flow<String?> = context.dataStore.data.map { prefs ->
        prefs[keyWsUrl]
    }

    suspend fun saveWsUrl(wsUrl: String) {
        context.dataStore.edit { it[keyWsUrl] = wsUrl }
    }

    suspend fun clear() {
        context.dataStore.edit { it.remove(keyWsUrl) }
    }
}