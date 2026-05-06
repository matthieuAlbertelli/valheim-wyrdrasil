using System.Xml.Serialization;

namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionPieceProgressData
{
    public int PieceId { get; set; }
    public bool MaterialsReserved { get; set; }
    public float CurrentWork { get; set; }
    public float RequiredWork { get; set; } = 1f;
    public ConstructionPieceBuildState State { get; set; } = ConstructionPieceBuildState.Pending;
    public ConstructionPieceStabilityLevel LastStabilityLevel { get; set; } = ConstructionPieceStabilityLevel.Unknown;
    public string LinkedWorldObjectName { get; set; } = string.Empty;
    public int BuildWave { get; set; } = -1;

    [XmlIgnore]
    public bool IsBuilt
    {
        get => State == ConstructionPieceBuildState.Built;
        set => State = value ? ConstructionPieceBuildState.Built : ConstructionPieceBuildState.Pending;
    }
}
