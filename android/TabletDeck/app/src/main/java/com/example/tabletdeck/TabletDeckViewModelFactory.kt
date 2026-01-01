package com.example.tabletdeck

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider

class TabletDeckViewModelFactory(
    private val appContext: Context
) : ViewModelProvider.Factory {

    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        if (modelClass.isAssignableFrom(TabletDeckViewModel::class.java)) {
            return TabletDeckViewModel(
                appContext = appContext.applicationContext,
                pairingStore = PairingStore(appContext.applicationContext)
            ) as T
        }
        throw IllegalArgumentException("Unknown ViewModel class: $modelClass")
    }
}
