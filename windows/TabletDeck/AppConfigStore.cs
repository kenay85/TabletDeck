using System.Text.Json;

namespace TabletDeck;

/// <summary>
/// Konfiguracja TabletDeck: katalog kafelków + profile layoutów.
/// Plik: %AppData%\TabletDeck\config.json
/// </summary>
internal static class AppConfigStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string ConfigPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TabletDeck", "config.json");

    public static AppConfig LoadOrCreate()
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg is not null && cfg.Catalog.Count > 0 && cfg.Profiles.Count > 0)
                    {
                        cfg = Normalize(cfg);
                        return cfg;
                    }
                }
                catch { }
            }

            var def = CreateDefault();
            Save(def);
            return def;
        }
    }

    public static void Save(AppConfig cfg)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));
        }
    }

    public static AppConfig Normalize(AppConfig cfg)
    {
        var profiles = cfg.Profiles
            .Select(p => NormalizeProfile(p))
            .ToList();

        var active = profiles.Any(p => p.Id == cfg.ActiveProfileId)
            ? cfg.ActiveProfileId
            : profiles[0].Id;

        // Usuń przypisania do akcji, których nie ma w katalogu (żeby nie wisiały “sieroty”)
        var allowed = new HashSet<string>(cfg.Catalog.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
        profiles = profiles.Select(p =>
        {
            var cells = p.Cells.Select(id => id is null ? null : (allowed.Contains(id) ? id : null)).ToList();
            return p with { Cells = cells };
        }).ToList();

        return cfg with { ActiveProfileId = active, Profiles = profiles, Language = Localization.NormalizeLanguageCode(cfg.Language) };
    }

    public static ProfileLayout NormalizeProfile(ProfileLayout p)
    {
        var rows = Math.Clamp(p.Rows, 1, 12);
        var cols = Math.Clamp(p.Cols, 1, 12);
        var need = rows * cols;

        var cells = p.Cells?.ToList() ?? new List<string?>();
        if (cells.Count < need)
            cells.AddRange(Enumerable.Repeat<string?>(null, need - cells.Count));
        if (cells.Count > need)
            cells = cells.Take(need).ToList();

        return p with { Rows = rows, Cols = cols, Cells = cells };
    }

    public static AppConfig CreateDefault()
    {
        var catalog = new List<ActionItem>
        {
            new("launch:notepad", "Notatnik"),
            new("launch:calc", "Kalkulator"),
            new("launch:explorer", "Eksplorator"),
            new("launch:taskmgr", "Menedżer zadań"),
            new("launch:chrome", "Chrome"),
        };

        var p1 = NewProfile("Praca", rows: 4, cols: 6);
        p1.Cells[0] = "launch:notepad";
        p1.Cells[1] = "launch:calc";
        p1.Cells[2] = "launch:explorer";
        p1.Cells[3] = "launch:taskmgr";

        var p2 = NewProfile("Gaming", rows: 4, cols: 6);

        return new AppConfig(
            Version: 1,
            ActiveProfileId: p1.Id,
            Catalog: catalog,
            Profiles: new List<ProfileLayout> { p1, p2 }
        );
}

    public static ProfileLayout NewProfile(string name, int rows, int cols)
    {
        var id = Guid.NewGuid().ToString("N");
        var cells = Enumerable.Repeat<string?>(null, Math.Clamp(rows, 1, 12) * Math.Clamp(cols, 1, 12)).ToList();
        return new ProfileLayout(id, name, rows, cols, cells, TileHeightDp: 106, IconSizeDp: 82);

    }
}
