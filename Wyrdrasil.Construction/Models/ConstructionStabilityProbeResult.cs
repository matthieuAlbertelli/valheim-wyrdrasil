namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionStabilityProbeResult
{
    public ConstructionPieceStabilityLevel Level { get; set; } = ConstructionPieceStabilityLevel.Unknown;
    public bool IsBuildable { get; set; }
    public string Reason { get; set; } = string.Empty;
}
