using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public sealed class CraftStationAnchorProfile
{
    public string PrefabName { get; }
    public Vector3 LocalApproachPosition { get; }
    public Vector3 LocalUsePosition { get; }
    public Vector3 LocalUseForward { get; }

    public CraftStationAnchorProfile(string prefabName, Vector3 localApproachPosition, Vector3 localUsePosition, Vector3 localUseForward)
    {
        PrefabName = prefabName;
        LocalApproachPosition = localApproachPosition;
        LocalUsePosition = localUsePosition;
        localUseForward.y = 0f;
        LocalUseForward = localUseForward.sqrMagnitude > 0.0001f ? localUseForward.normalized : Vector3.forward;
    }
}
