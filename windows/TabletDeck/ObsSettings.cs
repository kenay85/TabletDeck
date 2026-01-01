using System;

namespace TabletDeck;

public sealed record ObsSettings(
    string Host,
    int Port,
    string? Password,
    bool Enabled
)
{
    public static ObsSettings Default => new("127.0.0.1", 4455, null, false);
}
