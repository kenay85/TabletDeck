using System.Text.Json;

namespace TabletDeck;

/// <summary>
/// Trwałe ustawienia serwera (token/port).
/// Plik: %AppData%\TabletDeck\server.json
/// </summary>
internal sealed record ServerSettings(string Token, int Port);

internal static class ServerSettingsStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string PathOnDisk =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TabletDeck",
            "server.json"
        );

    public static ServerSettings LoadOrCreate(int defaultPort)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathOnDisk)!);

            if (File.Exists(PathOnDisk))
            {
                try
                {
                    var json = File.ReadAllText(PathOnDisk);
                    var s = JsonSerializer.Deserialize<ServerSettings>(json);
                    if (s is not null && !string.IsNullOrWhiteSpace(s.Token) && s.Port > 0)
                        return s;
                }
                catch { }
            }

            var created = new ServerSettings(TokenUtil.NewToken(), defaultPort);
            Save(created);
            return created;
        }
    }

    public static void Save(ServerSettings s)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathOnDisk)!);
            File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(s, JsonOpts));
        }
    }

    public static void Reset(int port)
    {
        Save(new ServerSettings(TokenUtil.NewToken(), port));
    }
}
