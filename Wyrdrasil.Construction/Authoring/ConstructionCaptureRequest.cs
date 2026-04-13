using UnityEngine;

namespace Wyrdrasil.Construction.Authoring;

public sealed class ConstructionCaptureRequest
{
    public Vector3 Center { get; set; }
    public Vector3 Size { get; set; }
    public Quaternion Rotation { get; set; } = Quaternion.identity;
    public string DisplayName { get; set; } = string.Empty;
}
