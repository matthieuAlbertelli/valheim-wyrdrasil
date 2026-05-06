namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionPreviewPieceEvaluation
{
    public int PieceId { get; set; }
    public ConstructionPieceBuildState State { get; set; } = ConstructionPieceBuildState.Pending;
    public ConstructionPieceStabilityLevel StabilityLevel { get; set; } = ConstructionPieceStabilityLevel.Unknown;
    public int BuildWave { get; set; } = -1;
    public string Reason { get; set; } = string.Empty;

    public bool IsResolved => State == ConstructionPieceBuildState.Buildable || State == ConstructionPieceBuildState.Built;
}