using System.Collections.Generic;

namespace TabletDeck;

/// <summary>Pozycja w katalogu (lista gotowych kafelków).</summary>
public sealed class ActionItem
{
    public string Id { get; init; }
    public string Label { get; set; }

    public ActionItem(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public override string ToString() => $"{Label} [{Id}]";
}

/// <summary>Układ kafelków dla profilu.</summary>
public sealed record ProfileLayout(
    string Id,
    string Name,
    int Rows,
    int Cols,
    List<string?> Cells,
    int TileHeightDp = 126,   // NOWE
    int IconSizeDp = 82       // NOWE
)
{
    public ProfileLayout() : this("", "Profil", 3, 5, new List<string?>(), 126, 82) { }
}


/// <summary>Konfiguracja aplikacji (katalog + profile).</summary>
public sealed record AppConfig(
    int Version,
    string ActiveProfileId,
    List<ActionItem> Catalog,
    List<ProfileLayout> Profiles)
{
    public string Language { get; init; } = Localization.DefaultLanguage;

    public AppConfig() : this(1, "", new List<ActionItem>(), new List<ProfileLayout>()) { }
}
