using System;
using System.Collections.Generic;
using System.Globalization;

namespace TabletDeck;

/// <summary>
/// Runtime localization (no resx) with safe fallback:
/// - Primary: current language
/// - Fallback: English ("en")
/// - Last resort: the key itself
/// </summary>
internal static class Localization
{
    public const string DefaultLanguage = "en";

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = new(StringComparer.Ordinal)
        {
            ["win.tray.tooltip"] = "TabletDeck PC",
            ["win.tray.showQr"] = "Show QR",
            ["win.tray.editor"] = "Tile editor",
            ["win.tray.copyUrl"] = "Copy pairing address",
            ["win.tray.sendToTablet"] = "Send file to tablet…",
            ["win.tray.sendToAndroid"] = "Send to Android",

            ["win.send.dialogTitle"] = "Select files to send to tablet",
            ["win.send.noClient"] = "No tablet connected.",
            ["win.send.sent"] = "Sending started.",
            ["win.send.failed"] = "Failed to send file(s): {0}",
            ["win.tray.buyCoffee"] = "Buy me a coffee",
            ["win.tray.obs"] = "OBS...",
            ["win.tray.language"] = "Language",
            ["win.tray.exit"] = "Exit",

            ["win.qr.title"] = "Pairing (QR)",
            ["win.qr.hint"] = "Scan this QR code on the tablet.\nWorks only on the same Wi‑Fi network.",
            ["win.qr.address"] = "Pairing address:",
            ["win.qr.regenerate"] = "Regenerate",
            ["win.qr.regenerate.noAltIp"] = "No alternative local IP found. Disable VPN or use another network interface.",

            ["win.obs.title"] = "TabletDeck — OBS",
            ["win.obs.enabled"] = "Enable OBS integration",
            ["win.obs.host"] = "Host:",
            ["win.obs.port"] = "Port:",
            ["win.obs.password"] = "Password:",
            ["win.common.save"] = "Save",
            ["win.common.cancel"] = "Cancel",
            ["win.common.confirm"] = "Confirm",

            ["win.addTile.title"] = "Add tile",
            ["win.addTile.hint"] = "Type a name. The app will suggest installed applications (Start Menu).",
            ["win.addTile.name"] = "Name",
            ["win.addTile.choose"] = "Choose application / shortcut",
            ["win.addTile.suggestions"] = "Suggestions",
            ["win.addTile.browseFile"] = "Browse file...",
            ["win.common.nameEmpty"] = "Name cannot be empty.",
            ["win.addTile.idHint"] = "Enter action ID (e.g., launch:notepad) or select an app from the list / file.",

            ["win.rename.title"] = "Rename tile",
            ["win.rename.name"] = "New name",

            ["win.editor.title"] = "TabletDeck — Screen editor",
            ["win.editor.addProgram"] = "+ Program",
            ["win.editor.search"] = "Search...",
            ["win.editor.screen"] = "Screen:",
            ["win.editor.rows"] = "Rows:",
            ["win.editor.cols"] = "Columns:",
            ["win.editor.tileHeight"] = "Tile height:",
            ["win.editor.iconSize"] = "Icon size:",
            ["win.editor.hint"] = "Drag an action onto a tile.\nRight‑click a tile: clear.",
            ["win.editor.addScreen"] = "+ Screen",
            ["win.editor.duplicate"] = "Duplicate",
            ["win.editor.rename"] = "Rename",
            ["win.editor.delete"] = "Remove",
            ["win.editor.clearTile"] = "Clear",
            ["win.editor.removeFromCatalog"] = "Remove from catalog",
            ["win.editor.needOneScreen"] = "At least one screen must exist.",
            ["win.editor.newScreenName"] = "New screen name:",
            ["win.editor.newScreen"] = "New screen",
            ["win.editor.renameScreenName"] = "New screen name:",
            ["win.editor.programName"] = "Program name:",
            ["win.editor.removeScreenConfirm"] = "Remove screen '{0}'?",
        },

        ["pl"] = new(StringComparer.Ordinal)
        {
            ["win.tray.tooltip"] = "TabletDeck PC",
            ["win.tray.showQr"] = "Pokaż QR",
            ["win.tray.editor"] = "Edytor kafelków",
            ["win.tray.copyUrl"] = "Kopiuj adres parowania",
            ["win.tray.sendToTablet"] = "Wyślij plik do tabletu…",
            ["win.tray.sendToAndroid"] = "Wyślij na Androida",

            ["win.send.dialogTitle"] = "Wybierz pliki do wysłania na tablet",
            ["win.send.noClient"] = "Brak połączonego tabletu.",
            ["win.send.sent"] = "Rozpoczęto wysyłanie.",
            ["win.send.failed"] = "Nie udało się wysłać plików: {0}",
            ["win.tray.buyCoffee"] = "Postaw kawę",
            ["win.tray.obs"] = "OBS...",
            ["win.tray.language"] = "Język",
            ["win.tray.exit"] = "Wyjście",

            ["win.qr.title"] = "Parowanie (QR)",
            ["win.qr.hint"] = "Zeskanuj ten kod QR na tablecie.\nDziała tylko w tej samej sieci Wi‑Fi.",
            ["win.qr.address"] = "Adres parowania:",
            ["win.qr.regenerate"] = "Wygeneruj ponownie",
            ["win.qr.regenerate.noAltIp"] = "Nie znaleziono innego lokalnego adresu IP. Wyłącz VPN lub użyj innego interfejsu sieciowego.",

            ["win.obs.title"] = "TabletDeck — OBS",
            ["win.obs.enabled"] = "Włącz integrację z OBS",
            ["win.obs.host"] = "Host:",
            ["win.obs.port"] = "Port:",
            ["win.obs.password"] = "Hasło:",
            ["win.common.save"] = "Zapisz",
            ["win.common.cancel"] = "Anuluj",
            ["win.common.confirm"] = "Potwierdź",

            ["win.addTile.title"] = "Dodaj kafelek",
            ["win.addTile.hint"] = "Wpisz nazwę. Program podpowie aplikacje zainstalowane w systemie (Start Menu).",
            ["win.addTile.name"] = "Nazwa",
            ["win.addTile.choose"] = "Wybierz aplikację / skrót",
            ["win.addTile.suggestions"] = "Podpowiedzi",
            ["win.addTile.browseFile"] = "Wybierz plik...",
            ["win.common.nameEmpty"] = "Nazwa nie może być pusta.",
            ["win.addTile.idHint"] = "Podaj ID akcji (np. launch:notepad) albo wybierz aplikację z listy / pliku.",

            ["win.rename.title"] = "Zmień nazwę",
            ["win.rename.name"] = "Nowa nazwa",

            ["win.editor.title"] = "TabletDeck — Edytor ekranów",
            ["win.editor.addProgram"] = "+ Program",
            ["win.editor.search"] = "Szukaj...",
            ["win.editor.screen"] = "Ekran:",
            ["win.editor.rows"] = "Wiersze:",
            ["win.editor.cols"] = "Kolumny:",
            ["win.editor.tileHeight"] = "Wysokość kafla:",
            ["win.editor.iconSize"] = "Rozmiar ikony:",
            ["win.editor.hint"] = "Przeciągnij akcję na kafel.\nPPM na kaflu: wyczyść.",
            ["win.editor.addScreen"] = "+ Ekran",
            ["win.editor.duplicate"] = "Duplikuj",
            ["win.editor.rename"] = "Zmień nazwę",
            ["win.editor.delete"] = "Usuń",
            ["win.editor.clearTile"] = "Wyczyść",
            ["win.editor.removeFromCatalog"] = "Usuń z katalogu",
            ["win.editor.needOneScreen"] = "Musi istnieć przynajmniej 1 ekran.",
            ["win.editor.newScreenName"] = "Nazwa nowego ekranu:",
            ["win.editor.newScreen"] = "Nowy ekran",
            ["win.editor.renameScreenName"] = "Nowa nazwa ekranu:",
            ["win.editor.programName"] = "Nazwa programu:",
            ["win.editor.removeScreenConfirm"] = "Usunąć ekran '{0}'?",
        },

        ["de"] = new(StringComparer.Ordinal)
        {
            ["win.tray.showQr"] = "QR anzeigen",
            ["win.tray.editor"] = "Kachel-Editor",
            ["win.tray.copyUrl"] = "Pairing-Adresse kopieren",
            ["win.tray.language"] = "Sprache",
            ["win.tray.exit"] = "Beenden",
            ["win.qr.title"] = "Kopplung (QR)",
            ["win.qr.hint"] = "Scanne diesen QR-Code auf dem Tablet.\nFunktioniert nur im selben WLAN.",
            ["win.common.save"] = "Speichern",
            ["win.common.cancel"] = "Abbrechen",
        },

        ["fr"] = new(StringComparer.Ordinal)
        {
            ["win.tray.showQr"] = "Afficher le QR",
            ["win.tray.editor"] = "Éditeur de tuiles",
            ["win.tray.copyUrl"] = "Copier l'adresse d'appairage",
            ["win.tray.language"] = "Langue",
            ["win.tray.exit"] = "Quitter",
            ["win.qr.title"] = "Appairage (QR)",
            ["win.qr.hint"] = "Scannez ce QR code sur la tablette.\nFonctionne uniquement sur le même Wi‑Fi.",
            ["win.common.save"] = "Enregistrer",
            ["win.common.cancel"] = "Annuler",
        },

        ["es"] = new(StringComparer.Ordinal)
        {
            ["win.tray.showQr"] = "Mostrar QR",
            ["win.tray.editor"] = "Editor de mosaicos",
            ["win.tray.copyUrl"] = "Copiar dirección de emparejamiento",
            ["win.tray.language"] = "Idioma",
            ["win.tray.exit"] = "Salir",
            ["win.qr.title"] = "Emparejamiento (QR)",
            ["win.qr.hint"] = "Escanea este QR en la tablet.\nSolo funciona en la misma Wi‑Fi.",
            ["win.common.save"] = "Guardar",
            ["win.common.cancel"] = "Cancelar",
        },

        ["it"] = new(StringComparer.Ordinal)
        {
            ["win.tray.showQr"] = "Mostra QR",
            ["win.tray.editor"] = "Editor tessere",
            ["win.tray.copyUrl"] = "Copia indirizzo di pairing",
            ["win.tray.language"] = "Lingua",
            ["win.tray.exit"] = "Esci",
            ["win.qr.title"] = "Abbinamento (QR)",
            ["win.qr.hint"] = "Scansiona questo QR sul tablet.\nFunziona solo sulla stessa Wi‑Fi.",
            ["win.common.save"] = "Salva",
            ["win.common.cancel"] = "Annulla",
        },

        ["uk"] = new(StringComparer.Ordinal)
        {
            ["win.tray.showQr"] = "Показати QR",
            ["win.tray.editor"] = "Редактор плиток",
            ["win.tray.copyUrl"] = "Копіювати адресу підключення",
            ["win.tray.language"] = "Мова",
            ["win.tray.exit"] = "Вийти",
            ["win.qr.title"] = "Підключення (QR)",
            ["win.qr.hint"] = "Скануйте цей QR-код на планшеті.\nПрацює лише в тій самій Wi‑Fi мережі.",
            ["win.common.save"] = "Зберегти",
            ["win.common.cancel"] = "Скасувати",
        },
    };

    private static string _language = DefaultLanguage;

    public static event EventHandler? LanguageChanged;

    public static string LanguageCode => _language;

    public static IReadOnlyList<(string Code, string NativeName)> SupportedLanguages { get; } =
        new List<(string, string)>
        {
            ("en", "English"),
            ("pl", "Polski"),
            ("de", "Deutsch"),
            ("fr", "Français"),
            ("es", "Español"),
            ("it", "Italiano"),
            ("uk", "Українська"),
        };

    public static string NormalizeLanguageCode(string? code)
    {
        var c = (code ?? "").Trim();
        if (string.IsNullOrWhiteSpace(c))
            return DefaultLanguage;

        foreach (var (langCode, _) in SupportedLanguages)
        {
            if (string.Equals(langCode, c, StringComparison.OrdinalIgnoreCase))
                return langCode;
        }

        // also accept culture-like "en-US"
        try
        {
            var ci = CultureInfo.GetCultureInfo(c);
            var two = ci.TwoLetterISOLanguageName;
            foreach (var (langCode, _) in SupportedLanguages)
            {
                if (string.Equals(langCode, two, StringComparison.OrdinalIgnoreCase))
                    return langCode;
            }
        }
        catch { }

        return DefaultLanguage;
    }

    public static void SetLanguage(string? code)
    {
        var normalized = NormalizeLanguageCode(code);
        if (string.Equals(_language, normalized, StringComparison.OrdinalIgnoreCase))
            return;

        _language = normalized;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string key) => T(key, Array.Empty<object>());

    public static string T(string key, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        var text = Resolve(key) ?? key;

        if (args.Length == 0)
            return text;

        try { return string.Format(CultureInfo.CurrentCulture, text, args); }
        catch { return text; }
    }

    private static string? Resolve(string key)
    {
        if (Strings.TryGetValue(_language, out var lang) && lang.TryGetValue(key, out var s))
            return s;

        if (Strings.TryGetValue(DefaultLanguage, out var en) && en.TryGetValue(key, out var se))
            return se;

        return null;
    }
}
