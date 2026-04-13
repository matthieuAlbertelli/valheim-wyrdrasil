namespace Wyrdrasil.Construction.Tool;

public sealed class ConstructionProjectStateSnapshot
{
    public int ProjectId { get; set; }
    public string State { get; set; } = string.Empty;
    public int BuiltPieceCount { get; set; }
    public int TotalPieceCount { get; set; }
}
