using System.Text.Json;

namespace TabletDeck;

internal static class ObsSettingsStore
{
    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TabletDeck",
            "obs.json"
        );

    public static ObsSettings Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return ObsSettings.Default;

                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ObsSettings>(json, JsonOptions) ?? ObsSettings.Default;
            }
            catch
            {
                return ObsSettings.Default;
            }
        }
    }

    public static void Save(ObsSettings settings)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // celowo: brak crasha przy zapisie ustawieñ
            }
        }
    }
}