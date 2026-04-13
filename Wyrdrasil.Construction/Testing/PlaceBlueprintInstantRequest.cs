using UnityEngine;

namespace Wyrdrasil.Construction.Testing;

public sealed class PlaceBlueprintInstantRequest
{
    public string BlueprintId { get; set; } = string.Empty;
    public Vector3 OriginPosition { get; set; }
    public Quaternion OriginRotation { get; set; } = Quaternion.identity;
    public bool IgnoreMaterials { get; set; } = true;
}
