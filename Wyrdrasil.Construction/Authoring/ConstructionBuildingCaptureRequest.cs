using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Construction.Authoring;

public sealed class ConstructionBuildingCaptureRequest
{
    public BuildingData Building { get; set; } = null!;
    public Vector3 OriginPosition { get; set; }
    public string BlueprintId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
