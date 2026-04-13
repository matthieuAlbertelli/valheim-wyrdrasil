using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Construction.Authoring;

public sealed class ConstructionZoneCaptureRequest
{
    public FunctionalZoneData Zone { get; set; } = null!;
    public Vector3 OriginPosition { get; set; }
    public string BlueprintId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}