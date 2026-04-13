using UnityEngine;

namespace Wyrdrasil.Construction.Authoring;

public sealed class CreateConstructionProjectRequest
{
    public string BlueprintId { get; set; } = string.Empty;
    public Vector3 OriginPosition { get; set; }
    public Quaternion OriginRotation { get; set; } = Quaternion.identity;
}
