// ===============================
// File: /TabletDeck/LayoutModels.cs
// ===============================
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace TabletDeck;

public sealed record LayoutConfig(
    [property: JsonPropertyName("rows")] int Rows,
    [property: JsonPropertyName("cols")] int Cols,
    [property: JsonPropertyName("catalog")] List<ActionItem> Catalog,
    [property: JsonPropertyName("cells")] List<string?> Cells
);
