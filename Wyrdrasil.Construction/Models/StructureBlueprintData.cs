using System.Collections.Generic;

namespace Wyrdrasil.Construction.Models;

public sealed class StructureBlueprintData
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public List<BlueprintPieceData> Pieces { get; set; } = new();
}
