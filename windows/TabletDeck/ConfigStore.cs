using System.Text.Json;

namespace TabletDeck;

/// <summary>Trzyma layout/katalog kafelków w %AppData%\TabletDeck\layout.json.</summary>
internal static class ConfigStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string LayoutPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TabletDeck", "layout.json");

    public static LayoutConfig LoadOrCreate()
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);

            if (File.Exists(LayoutPath))
            {
                try
                {
                    var json = File.ReadAllText(LayoutPath);
                    var cfg = JsonSerializer.Deserialize<LayoutConfig>(json);
                    if (cfg is not null)
                        return NormalizeAndMigrate(cfg);
                }
                catch { }
            }

            var def = CreateDefault();
            Save(def);
            return def;
        }
    }

    public static void Save(LayoutConfig cfg)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath)!);
            File.WriteAllText(LayoutPath, JsonSerializer.Serialize(cfg, JsonOpts));
        }
    }

    public static LayoutConfig CreateDefault()
    {
        var catalog = new List<ActionItem>
        {
            new("launch:notepad", "Notatnik"),
            new("launch:calc", "Kalkulator"),
            new("launch:explorer", "Eksplorator plików"),
            new("launch:taskmgr", "Menedżer zadań"),
            new("launch:chrome", "Chrome"),
        };

        const int rows = 4;
        const int cols = 5; // u Ciebie na Androidzie jest 4x5
        var cells = Enumerable.Repeat<string?>(null, rows * cols).ToList();

        cells[0] = "launch:notepad";
        cells[1] = "launch:calc";

        var p1 = NewProfile("Ekran 1", rows, cols);
        p1.Cells[0] = cells[0];
        p1.Cells[1] = cells[1];

        return new LayoutConfig(
            Rows: rows,
            Cols: cols,
            Catalog: catalog,
            Cells: cells,
            Profiles: new List<ProfileLayout> { p1 },
            ActiveProfileId: p1.Id
        );
    }

    public static ProfileLayout NewProfile(string name, int rows, int cols)
    {
        var id = Guid.NewGuid().ToString("N");
        rows = Math.Clamp(rows, 1, 12);
        cols = Math.Clamp(cols, 1, 12);
        var cells = Enumerable.Repeat<string?>(null, rows * cols).ToList();
        return new ProfileLayout(id, name, rows, cols, cells);
    }

    private static LayoutConfig NormalizeAndMigrate(LayoutConfig cfg)
    {
        // 1) normalizacja starego układu
        cfg = NormalizeLegacy(cfg);

        // 2) migracja: jeśli brak Profiles -> zrób jeden profil z Rows/Cols/Cells
        if (cfg.Profiles is null || cfg.Profiles.Count == 0)
        {
            var p = new ProfileLayout(
                Id: Guid.NewGuid().ToString("N"),
                Name: "Ekran 1",
                Rows: cfg.Rows,
                Cols: cfg.Cols,
                Cells: cfg.Cells.ToList()
            );

            cfg = cfg with
            {
                Profiles = new List<ProfileLayout> { p },
                ActiveProfileId = p.Id
            };
        }

        // 3) upewnij się, że ActiveProfileId jest poprawne
        var profiles = cfg.Profiles!;
        var activeId = cfg.ActiveProfileId;
        if (string.IsNullOrWhiteSpace(activeId) || profiles.All(p => p.Id != activeId))
            activeId = profiles[0].Id;

        // 4) znormalizuj profile (rozmiary i liczba komórek)
        profiles = profiles
            .Select(p => NormalizeProfile(p))
            .ToList();

        // 5) zachowaj kompatybilność: Rows/Cols/Cells = aktywny profil (dla starego kodu)
        var active = profiles.First(p => p.Id == activeId);

        return cfg with
        {
            Profiles = profiles,
            ActiveProfileId = activeId,
            Rows = active.Rows,
            Cols = active.Cols,
            Cells = active.Cells.ToList(),
        };
    }

    private static LayoutConfig NormalizeLegacy(LayoutConfig cfg)
    {
        var r = Math.Clamp(cfg.Rows, 1, 12);
        var c = Math.Clamp(cfg.Cols, 1, 12);

        var need = r * c;
        var cells = cfg.Cells ?? new List<string?>();
        if (cells.Count != need)
        {
            var fixedCells = Enumerable.Repeat<string?>(null, need).ToList();
            for (var i = 0; i < Math.Min(need, cells.Count); i++)
                fixedCells[i] = cells[i];
            cells = fixedCells;
        }

        return cfg with { Rows = r, Cols = c, Cells = cells };
    }

    private static ProfileLayout NormalizeProfile(ProfileLayout p)
    {
        var r = Math.Clamp(p.Rows, 1, 12);
        var c = Math.Clamp(p.Cols, 1, 12);
        var need = r * c;

        var cells = p.Cells ?? new List<string?>();
        if (cells.Count != need)
        {
            var fixedCells = Enumerable.Repeat<string?>(null, need).ToList();
            for (var i = 0; i < Math.Min(need, cells.Count); i++)
                fixedCells[i] = cells[i];
            cells = fixedCells;
        }

        return p with { Rows = r, Cols = c, Cells = cells };
    }

    // Pomocnicze: zwraca aktywny profil + zapisuje zmiany w profilu
    public static (LayoutConfig cfg, ProfileLayout active) GetActive(LayoutConfig cfg)
    {
        cfg = NormalizeAndMigrate(cfg);
        var active = cfg.Profiles!.First(p => p.Id == cfg.ActiveProfileId);
        return (cfg, active);
    }

    public static LayoutConfig UpsertProfile(LayoutConfig cfg, ProfileLayout updated)
    {
        cfg = NormalizeAndMigrate(cfg);
        var list = cfg.Profiles!.ToList();
        var idx = list.FindIndex(p => p.Id == updated.Id);
        if (idx >= 0) list[idx] = updated;
        else list.Add(updated);

        // kompatybilność: legacy = active
        if (updated.Id == cfg.ActiveProfileId)
        {
            cfg = cfg with { Rows = updated.Rows, Cols = updated.Cols, Cells = updated.Cells.ToList() };
        }

        return cfg with { Profiles = list };
    }

    public static LayoutConfig SetActive(LayoutConfig cfg, string profileId)
    {
        cfg = NormalizeAndMigrate(cfg);
        var p = cfg.Profiles!.FirstOrDefault(x => x.Id == profileId) ?? cfg.Profiles[0];
        return cfg with
        {
            ActiveProfileId = p.Id,
            Rows = p.Rows,
            Cols = p.Cols,
            Cells = p.Cells.ToList()
        };
    }
}