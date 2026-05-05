using UnityEngine;

namespace Wyrdrasil.Core.Tool;

/// <summary>
/// Runtime policy for approaching and engaging an occupation anchor.
///
/// The profile deliberately lives in Core so feature modules can share the same distances and timing
/// rules without duplicating seat/bed/slot-specific magic numbers inside actor AI components.
/// </summary>
public readonly struct OccupationAnchorApproachProfile
{
    public float ApproachRadius { get; }
    public float UseRadius { get; }
    public float ForcedAttemptRadiusFromApproach { get; }
    public float ForcedAttemptRadiusFromEngage { get; }
    public float AttemptRetryInterval { get; }
    public float ProgressEpsilon { get; }
    public float DirectApproachTimeout { get; }
    public float RouteApproachTimeout { get; }
    public float DirectStuckTimeout { get; }
    public float RouteStuckTimeout { get; }

    public OccupationAnchorApproachProfile(
        float approachRadius,
        float useRadius,
        float forcedAttemptRadiusFromApproach,
        float forcedAttemptRadiusFromEngage,
        float attemptRetryInterval,
        float progressEpsilon,
        float directApproachTimeout,
        float routeApproachTimeout,
        float directStuckTimeout,
        float routeStuckTimeout)
    {
        ApproachRadius = Mathf.Max(0.01f, approachRadius);
        UseRadius = Mathf.Max(0.01f, useRadius);
        ForcedAttemptRadiusFromApproach = Mathf.Max(ApproachRadius, forcedAttemptRadiusFromApproach);
        ForcedAttemptRadiusFromEngage = Mathf.Max(UseRadius, forcedAttemptRadiusFromEngage);
        AttemptRetryInterval = Mathf.Max(0.01f, attemptRetryInterval);
        ProgressEpsilon = Mathf.Max(0.001f, progressEpsilon);
        DirectApproachTimeout = Mathf.Max(0.01f, directApproachTimeout);
        RouteApproachTimeout = Mathf.Max(0.01f, routeApproachTimeout);
        DirectStuckTimeout = Mathf.Max(0.01f, directStuckTimeout);
        RouteStuckTimeout = Mathf.Max(0.01f, routeStuckTimeout);
    }

    public static OccupationAnchorApproachProfile SeatDefault => new(
        approachRadius: 0.75f,
        useRadius: 0.75f,
        forcedAttemptRadiusFromApproach: 1.25f,
        forcedAttemptRadiusFromEngage: 1.40f,
        attemptRetryInterval: 0.25f,
        progressEpsilon: 0.10f,
        directApproachTimeout: 1.35f,
        routeApproachTimeout: 0.75f,
        directStuckTimeout: 0.90f,
        routeStuckTimeout: 0.50f);

    public static OccupationAnchorApproachProfile BedDefault => new(
        approachRadius: 0.75f,
        useRadius: 0.75f,
        forcedAttemptRadiusFromApproach: 1.25f,
        forcedAttemptRadiusFromEngage: 1.40f,
        attemptRetryInterval: 0.25f,
        progressEpsilon: 0.10f,
        directApproachTimeout: 1.35f,
        routeApproachTimeout: 0.75f,
        directStuckTimeout: 0.90f,
        routeStuckTimeout: 0.50f);


    public static OccupationAnchorApproachProfile StandingPointDefault => new(
        approachRadius: 0.55f,
        useRadius: 0.70f,
        forcedAttemptRadiusFromApproach: 0.90f,
        forcedAttemptRadiusFromEngage: 1.00f,
        attemptRetryInterval: 0.25f,
        progressEpsilon: 0.08f,
        directApproachTimeout: 1.20f,
        routeApproachTimeout: 0.70f,
        directStuckTimeout: 0.80f,
        routeStuckTimeout: 0.50f);

    public static OccupationAnchorApproachProfile WorkPointDefault => new(
        approachRadius: 0.60f,
        useRadius: 0.75f,
        forcedAttemptRadiusFromApproach: 0.95f,
        forcedAttemptRadiusFromEngage: 1.10f,
        attemptRetryInterval: 0.25f,
        progressEpsilon: 0.08f,
        directApproachTimeout: 1.20f,
        routeApproachTimeout: 0.70f,
        directStuckTimeout: 0.80f,
        routeStuckTimeout: 0.50f);

    public float GetApproachTimeout(bool arrivedFromWaypointRoute)
    {
        return arrivedFromWaypointRoute ? RouteApproachTimeout : DirectApproachTimeout;
    }

    public float GetStuckTimeout(bool arrivedFromWaypointRoute)
    {
        return arrivedFromWaypointRoute ? RouteStuckTimeout : DirectStuckTimeout;
    }

    public bool CanForceAttempt(Vector3 actorPosition, Vector3 approachPosition, Vector3 engagePosition)
    {
        return HorizontalDistance(actorPosition, approachPosition) <= ForcedAttemptRadiusFromApproach ||
               HorizontalDistance(actorPosition, engagePosition) <= ForcedAttemptRadiusFromEngage;
    }

    public bool CanAttempt(Vector3 actorPosition, Vector3 approachPosition, Vector3 engagePosition)
    {
        return HorizontalDistance(actorPosition, approachPosition) <= UseRadius ||
               CanForceAttempt(actorPosition, approachPosition, engagePosition);
    }

    private static float HorizontalDistance(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        delta.y = 0f;
        return delta.magnitude;
    }
}
