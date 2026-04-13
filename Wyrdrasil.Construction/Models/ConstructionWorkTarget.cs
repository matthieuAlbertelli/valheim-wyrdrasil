using UnityEngine;

namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionWorkTarget
{
    public int ProjectId { get; set; }
    public int PieceId { get; set; }
    public Vector3 WorldPosition { get; set; }
}
