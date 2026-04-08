using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;


public sealed class BuildingData
{
    public int Id { get; }

    public string DisplayName { get; }

    public Vector3 AnchorPosition { get; }

    public BuildingData(int id, string displayName, Vector3 anchorPosition)
    {
        Id = id;
        DisplayName = displayName;
        AnchorPosition = anchorPosition;
    }
}
