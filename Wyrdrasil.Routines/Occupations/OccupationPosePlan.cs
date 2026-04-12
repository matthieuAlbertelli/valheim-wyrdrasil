using UnityEngine;

namespace Wyrdrasil.Routines.Occupations;

public readonly struct OccupationPosePlan
{
    public Vector3 ApproachPosition { get; }
    public Vector3 EngagePosition { get; }
    public Vector3 FacingDirection { get; }
    public float NavigationStopDistance { get; }
    public float EngageRadius { get; }
    public float SustainRadius { get; }

    public OccupationPosePlan(
        Vector3 approachPosition,
        Vector3 engagePosition,
        Vector3 facingDirection,
        float navigationStopDistance,
        float engageRadius,
        float sustainRadius)
    {
        ApproachPosition = approachPosition;
        EngagePosition = engagePosition;
        FacingDirection = facingDirection;
        NavigationStopDistance = navigationStopDistance;
        EngageRadius = engageRadius;
        SustainRadius = sustainRadius;
    }
}
