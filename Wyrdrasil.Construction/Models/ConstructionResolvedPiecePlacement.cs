using UnityEngine;

namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionResolvedPiecePlacement
{
    public int PieceId { get; set; }
    public string PrefabName { get; set; } = string.Empty;
    public GameObject Prefab { get; set; } = null!;
    public Vector3 WorldPosition { get; set; }
    public Quaternion WorldRotation { get; set; }
}
