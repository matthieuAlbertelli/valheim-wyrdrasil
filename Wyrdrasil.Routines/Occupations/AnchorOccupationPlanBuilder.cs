using UnityEngine;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AnchorOccupationPlanBuilder
{
    public OccupationPosePlan BuildPlan(OccupationAnchorDefinition anchorDefinition)
    {
        if (anchorDefinition.UsesExplicitApproachPosition)
        {
            var facingDirection = NormalizeFacingDirection(anchorDefinition.Pose.FacingDirection);
            return new OccupationPosePlan(
                anchorDefinition.ApproachPosition,
                anchorDefinition.Pose.EngagePosition,
                facingDirection,
                anchorDefinition.NavigationStopDistance,
                anchorDefinition.EngageRadius,
                anchorDefinition.SustainRadius);
        }

        return BuildPlan(
            anchorDefinition.Pose,
            anchorDefinition.ApproachDistance,
            anchorDefinition.NavigationStopDistance,
            anchorDefinition.EngageRadius,
            anchorDefinition.SustainRadius);
    }

    public OccupationPosePlan BuildPlan(
        OccupationAnchorPose anchorPose,
        float approachDistance,
        float navigationStopDistance,
        float engageRadius,
        float sustainRadius)
    {
        var facingDirection = NormalizeFacingDirection(anchorPose.FacingDirection);
        var approachPosition = anchorPose.EngagePosition - facingDirection * approachDistance;
        return new OccupationPosePlan(
            approachPosition,
            anchorPose.EngagePosition,
            facingDirection,
            navigationStopDistance,
            engageRadius,
            sustainRadius);
    }

    private static Vector3 NormalizeFacingDirection(Vector3 facingDirection)
    {
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return facingDirection.normalized;
    }
}
