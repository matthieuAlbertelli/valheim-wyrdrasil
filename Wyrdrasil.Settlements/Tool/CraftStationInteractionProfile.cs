using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public sealed class CraftStationInteractionProfile
{
    public string ProfileId { get; }
    public Vector3 DefaultLocalAnchorPosition { get; }
    public Vector3 DefaultLocalAnchorForward { get; }
    public float ApproachDistance { get; }
    public float NavigationStopDistance { get; }
    public float EngageRadius { get; }
    public float SustainRadius { get; }

    public CraftStationInteractionProfile(
        string profileId,
        Vector3 defaultLocalAnchorPosition,
        Vector3 defaultLocalAnchorForward,
        float approachDistance,
        float navigationStopDistance,
        float engageRadius,
        float sustainRadius)
    {
        ProfileId = profileId;
        DefaultLocalAnchorPosition = defaultLocalAnchorPosition;

        defaultLocalAnchorForward.y = 0f;
        DefaultLocalAnchorForward = defaultLocalAnchorForward.sqrMagnitude > 0.0001f
            ? defaultLocalAnchorForward.normalized
            : Vector3.forward;

        ApproachDistance = Mathf.Max(0f, approachDistance);
        NavigationStopDistance = Mathf.Max(0.05f, navigationStopDistance);
        EngageRadius = Mathf.Max(0.05f, engageRadius);
        SustainRadius = Mathf.Max(EngageRadius, sustainRadius);
    }
}
