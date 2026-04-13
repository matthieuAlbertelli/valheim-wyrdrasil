using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Construction.Models;

public sealed class BlueprintPieceData
{
    public int PieceId { get; set; }
    public string PrefabName { get; set; } = string.Empty;
    public Vector3 LocalPosition { get; set; }
    public Quaternion LocalRotation { get; set; }
    public int BuildOrder { get; set; }
    public List<int> DependencyPieceIds { get; set; } = new();
    public List<MaterialRequirementData> Materials { get; set; } = new();
}
