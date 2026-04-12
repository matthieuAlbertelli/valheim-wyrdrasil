using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationPlanBuilder
{
    public bool TryBuildPlan(RegisteredCraftStationData craftStationData, out OccupationPosePlan plan)
    {
        if (!craftStationData.TryResolveWorldAnchor(out var engagePosition, out var facingDirection))
        {
            plan = default;
            return false;
        }

        if (!CraftStationInteractionProfileRegistry.TryGetProfileById(craftStationData.InteractionProfileId, out var profile))
        {
            profile = CraftStationInteractionProfileRegistry.GetDefaultProfile();
        }

        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            facingDirection = Vector3.forward;
        }

        facingDirection.Normalize();
        var approachPosition = engagePosition - facingDirection * profile.ApproachDistance;
        plan = new OccupationPosePlan(
            approachPosition,
            engagePosition,
            facingDirection,
            profile.NavigationStopDistance,
            profile.EngageRadius,
            profile.SustainRadius);
        return true;
    }
}
