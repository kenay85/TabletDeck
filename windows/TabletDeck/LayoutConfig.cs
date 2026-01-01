namespace TabletDeck;

public sealed record ActionItem(string Id, string Label);

public sealed record LayoutConfig(
    int Rows,
    int Cols,
    List<ActionItem> Catalog,
    List<string?> Cells,

    // NOWE (wstecznie kompatybilne; stare jsony nie mają tych pól)
    List<ProfileLayout>? Profiles = null,
    string? ActiveProfileId = null
);