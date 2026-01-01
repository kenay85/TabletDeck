namespace TabletDeck;

public sealed record ProfileLayout(
    string Id,
    string Name,
    int Rows,
    int Cols,
    List<string?> Cells
);