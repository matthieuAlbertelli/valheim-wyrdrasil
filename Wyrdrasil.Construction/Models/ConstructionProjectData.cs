using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionProjectData
{
    public int Id { get; set; }
    public string BlueprintId { get; set; } = string.Empty;
    public Vector3 OriginPosition { get; set; }
    public Quaternion OriginRotation { get; set; }
    public ConstructionProjectState State { get; set; }
    public ConstructionProjectProgressData Progress { get; set; } = new();
    public List<ConstructionWorkPostData> WorkPosts { get; set; } = new();
    public List<MaterialLedgerEntryData> MaterialLedger { get; set; } = new();
}
