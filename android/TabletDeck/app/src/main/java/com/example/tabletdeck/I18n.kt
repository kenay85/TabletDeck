package com.example.tabletdeck

object I18n {
    private const val DEFAULT = "en"

    private val strings: Map<String, Map<String, String>> = mapOf(
        "en" to mapOf(
            "pairing.title" to "Connection",
            "pairing.status" to "Status: %s",
            "pairing.pc" to "PC: %s",
            "pairing.addr" to "Address: %s",
            "pairing.connect" to "Connect",
            "pairing.disconnect" to "Disconnect",
        "pairing.forget" to "Forget",
        "pairing.forgetTitle" to "Forget paired PC?",
        "pairing.forgetConfirm" to "This will remove the saved address. You can pair again by scanning the QR code.",
        "common.cancel" to "Cancel",
            "pairing.manualTitle" to "Manual entry (fallback)",
            "pairing.manualHint" to "ws://IP:PORT/ws?token=...",
            "pairing.qrScan" to "Scan QR",
            "pairing.qrPrompt" to "Scan the QR from the Windows app",
            "pairing.cameraRequired" to "Camera permission required.",
            "pairing.allowCamera" to "Allow camera",

            "status.disconnected" to "Disconnected",
            "status.connecting" to "Connecting…",
            "status.connected" to "Connected",
            "status.error" to "Error: %s",

            "file.title" to "Send a file to PC",
            "file.pick" to "Choose file",
            "file.send" to "Send",
            "file.chosen" to "Selected: %s",
            "file.sent" to "Sent: %s",
            "file.err" to "Error: %s",
            "file.dest" to "Destination: Downloads/TabletDeckUploads (Windows).",

            "stats.title" to "PC stats",
            "chart.noData" to "No data",

            "media.title" to "Media controls",
            "obs.title" to "OBS Studio",
            "btn.reset" to "Reset",
            "btn.playPause" to "Play/Pause",
            "btn.next" to "Next",
            "btn.stop" to "Stop",
            "btn.mute" to "Mute",
            "btn.volDown" to "Vol-",
            "btn.volUp" to "Vol+",
            "btn.streamStart" to "Stream START",
            "btn.streamStop" to "Stream STOP",
            "btn.recordStart" to "Record START",
            "btn.recordStop" to "Record STOP",
            "btn.scenePrev" to "Scene -",
            "btn.sceneNext" to "Scene +",
            "btn.micMute" to "Mic Mute",
            "btn.desktopMute" to "Desktop Mute",
            "btn.replaySave" to "Replay Save",
        ),

        "pl" to mapOf(
            "pairing.title" to "Połączenie",
            "pairing.status" to "Status: %s",
            "pairing.pc" to "PC: %s",
            "pairing.addr" to "Adres: %s",
            "pairing.connect" to "Połącz",
            "pairing.disconnect" to "Rozłącz",
        "pairing.forget" to "Zapomnij",
        "pairing.forgetTitle" to "Zapomnieć sparowany komputer?",
        "pairing.forgetConfirm" to "To usunie zapisany adres. Możesz ponownie sparować skanując kod QR.",
        "common.cancel" to "Anuluj",
            "pairing.manualTitle" to "Ręczne wpisanie (awaryjne)",
            "pairing.manualHint" to "ws://IP:PORT/ws?token=...",
            "pairing.qrScan" to "Skanuj QR",
            "pairing.qrPrompt" to "Zeskanuj QR z aplikacji na Windows",
            "pairing.cameraRequired" to "Wymagana zgoda na kamerę.",
            "pairing.allowCamera" to "Zezwól na kamerę",

            "status.disconnected" to "Rozłączono",
            "status.connecting" to "Łączenie…",
            "status.connected" to "Połączono",
            "status.error" to "Błąd: %s",

            "file.title" to "Wyślij plik na komputer",
            "file.pick" to "Wybierz plik",
            "file.send" to "Wyślij",
            "file.chosen" to "Wybrano: %s",
            "file.sent" to "Wysłano: %s",
            "file.err" to "Błąd: %s",
            "file.dest" to "Docelowo: Downloads/TabletDeckUploads (Windows).",

            "stats.title" to "Statystyki PC",
            "chart.noData" to "Brak danych",

            "media.title" to "Sterowanie mediami",
            "obs.title" to "OBS Studio",
            "btn.reset" to "Reset",
            "btn.playPause" to "Play/Pause",
            "btn.next" to "Next",
            "btn.stop" to "Stop",
            "btn.mute" to "Mute",
            "btn.volDown" to "Vol-",
            "btn.volUp" to "Vol+",
            "btn.streamStart" to "Stream START",
            "btn.streamStop" to "Stream STOP",
            "btn.recordStart" to "Record START",
            "btn.recordStop" to "Record STOP",
            "btn.scenePrev" to "Scene -",
            "btn.sceneNext" to "Scene +",
            "btn.micMute" to "Mic Mute",
            "btn.desktopMute" to "Desktop Mute",
            "btn.replaySave" to "Replay Save",
        ),

        "de" to mapOf(
            "pairing.title" to "Verbindung",
            "pairing.connect" to "Verbinden",
            "pairing.disconnect" to "Trennen",
            "pairing.manualTitle" to "Manuelle Eingabe (Fallback)",
            "pairing.qrScan" to "QR scannen",
            "pairing.qrPrompt" to "Scanne den QR aus der Windows-App",
            "pairing.cameraRequired" to "Kameraberechtigung erforderlich.",
            "pairing.allowCamera" to "Kamera erlauben",
            "status.disconnected" to "Getrennt",
            "status.connecting" to "Verbinde…",
            "status.connected" to "Verbunden",
            "status.error" to "Fehler: %s",
            "file.title" to "Datei an PC senden",
            "file.pick" to "Datei wählen",
            "file.send" to "Senden",
            "chart.noData" to "Keine Daten",
            "stats.title" to "PC-Statistiken",
            "media.title" to "Mediensteuerung",
        ),

        "fr" to mapOf(
            "pairing.title" to "Connexion",
            "pairing.connect" to "Connecter",
            "pairing.disconnect" to "Déconnecter",
            "pairing.manualTitle" to "Saisie manuelle (secours)",
            "pairing.qrScan" to "Scanner QR",
            "pairing.qrPrompt" to "Scannez le QR de l'app Windows",
            "pairing.cameraRequired" to "Autorisation caméra requise.",
            "pairing.allowCamera" to "Autoriser la caméra",
            "status.disconnected" to "Déconnecté",
            "status.connecting" to "Connexion…",
            "status.connected" to "Connecté",
            "status.error" to "Erreur : %s",
            "file.title" to "Envoyer un fichier au PC",
            "file.pick" to "Choisir un fichier",
            "file.send" to "Envoyer",
            "chart.noData" to "Aucune donnée",
            "stats.title" to "Stats PC",
            "media.title" to "Contrôles média",
        ),

        "es" to mapOf(
            "pairing.title" to "Conexión",
            "pairing.connect" to "Conectar",
            "pairing.disconnect" to "Desconectar",
            "pairing.manualTitle" to "Entrada manual (respaldo)",
            "pairing.qrScan" to "Escanear QR",
            "pairing.qrPrompt" to "Escanea el QR de la app de Windows",
            "pairing.cameraRequired" to "Permiso de cámara requerido.",
            "pairing.allowCamera" to "Permitir cámara",
            "status.disconnected" to "Desconectado",
            "status.connecting" to "Conectando…",
            "status.connected" to "Conectado",
            "status.error" to "Error: %s",
            "file.title" to "Enviar archivo al PC",
            "file.pick" to "Elegir archivo",
            "file.send" to "Enviar",
            "chart.noData" to "Sin datos",
            "stats.title" to "Estadísticas PC",
            "media.title" to "Controles multimedia",
        ),

        "it" to mapOf(
            "pairing.title" to "Connessione",
            "pairing.connect" to "Connetti",
            "pairing.disconnect" to "Disconnetti",
            "pairing.manualTitle" to "Inserimento manuale (fallback)",
            "pairing.qrScan" to "Scansiona QR",
            "pairing.qrPrompt" to "Scansiona il QR dall'app Windows",
            "pairing.cameraRequired" to "Permesso fotocamera richiesto.",
            "pairing.allowCamera" to "Consenti fotocamera",
            "status.disconnected" to "Disconnesso",
            "status.connecting" to "Connessione…",
            "status.connected" to "Connesso",
            "status.error" to "Errore: %s",
            "file.title" to "Invia file al PC",
            "file.pick" to "Scegli file",
            "file.send" to "Invia",
            "chart.noData" to "Nessun dato",
            "stats.title" to "Statistiche PC",
            "media.title" to "Controlli media",
        ),

        "uk" to mapOf(
            "pairing.title" to "Зʼєднання",
            "pairing.connect" to "Підʼєднатись",
            "pairing.disconnect" to "Відʼєднатись",
            "pairing.manualTitle" to "Ручне введення (резерв)",
            "pairing.qrScan" to "Сканувати QR",
            "pairing.qrPrompt" to "Скануйте QR з Windows-додатка",
            "pairing.cameraRequired" to "Потрібен дозвіл на камеру.",
            "pairing.allowCamera" to "Дозволити камеру",
            "status.disconnected" to "Відʼєднано",
            "status.connecting" to "Зʼєднання…",
            "status.connected" to "Підʼєднано",
            "status.error" to "Помилка: %s",
            "file.title" to "Надіслати файл на ПК",
            "file.pick" to "Вибрати файл",
            "file.send" to "Надіслати",
            "chart.noData" to "Немає даних",
            "stats.title" to "Статистика ПК",
            "media.title" to "Керування медіа",
        ),
    )

    fun normalize(lang: String?): String {
        val raw = (lang ?: "").trim().lowercase()
        return when {
            raw.isBlank() -> DEFAULT
            strings.containsKey(raw) -> raw
            raw.length >= 2 && strings.containsKey(raw.substring(0, 2)) -> raw.substring(0, 2)
            else -> DEFAULT
        }
    }

    fun t(lang: String?, key: String, vararg args: Any?): String {
        val lc = normalize(lang)
        val s = strings[lc]?.get(key)
            ?: strings[DEFAULT]?.get(key)
            ?: key

        return if (args.isEmpty()) s else try {
            String.format(s, *args)
        } catch (_: Throwable) {
            s
        }
    }
}
