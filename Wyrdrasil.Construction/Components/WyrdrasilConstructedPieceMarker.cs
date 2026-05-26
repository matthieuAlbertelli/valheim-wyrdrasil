using UnityEngine;

namespace Wyrdrasil.Construction.Components;

/// <summary>
/// Runtime marker attached to pieces that Wyrdrasil has either built itself or adopted from the world.
/// It is intentionally lightweight and non-authoritative: persistence remains in ConstructionProjectData,
/// while the marker gives runtime services an O(1)-friendly way to correlate native Valheim pieces with
/// blueprint pieces during the current session.
/// </summary>
public sealed class WyrdrasilConstructedPieceMarker : MonoBehaviour
{
    public int ProjectId { get; private set; }
    public int PieceId { get; private set; }
    public string BlueprintId { get; private set; } = string.Empty;
    public string PrefabName { get; private set; } = string.Empty;

    public void Initialize(int projectId, int pieceId, string blueprintId, string prefabName)
    {
        ProjectId = projectId;
        PieceId = pieceId;
        BlueprintId = blueprintId ?? string.Empty;
        PrefabName = prefabName ?? string.Empty;
    }
}
