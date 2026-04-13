using UnityEngine;

namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionWorkPostData
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Vector3 WorldPosition { get; set; }
    public Quaternion WorldRotation { get; set; }
    public int? AssignedResidentId { get; set; }
}
