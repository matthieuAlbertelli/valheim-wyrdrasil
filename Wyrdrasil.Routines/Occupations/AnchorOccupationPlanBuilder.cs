using Wyrdrasil.Core.Tool;
using UnityEngine;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AnchorOccupationPlanBuilder
{
    public OccupationPosePlan BuildPlan(
        OccupationAnchorPose anchorPose,
        float approachDistance,
        float navigationStopDistance,
        float engageRadius,
        float sustainRadius)
    {
        var facingDirection = anchorPose.FacingDirection;
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            facingDirection = Vector3.forward;
        }

        facingDirection.Normalize();
        var approachPosition = anchorPose.EngagePosition - facingDirection * approachDistance;
        return new OccupationPosePlan(
            approachPosition,
            anchorPose.EngagePosition,
            facingDirection,
            navigationStopDistance,
            engageRadius,
            sustainRadius);
    }
}
