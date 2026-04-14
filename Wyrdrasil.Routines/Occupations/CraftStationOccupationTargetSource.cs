using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationTargetSource : IOccupationTargetSource
{
    private readonly CraftStationService _craftStationService;
    private readonly AnchorOccupationPlanBuilder _planBuilder;

    public CraftStationOccupationTargetSource(CraftStationService craftStationService, AnchorOccupationPlanBuilder planBuilder)
    {
        _craftStationService = craftStationService;
        _planBuilder = planBuilder;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.CraftStation;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (!_craftStationService.TryGetCraftStationById(targetRef.TargetId, out var craftStationData) ||
            !craftStationData.TryResolveWorldAnchor(out var engagePosition, out var facingDirection))
        {
            target = null!;
            return false;
        }

        if (!CraftStationInteractionProfileRegistry.TryGetProfileById(craftStationData.InteractionProfileId, out var profile))
        {
            profile = CraftStationInteractionProfileRegistry.GetDefaultProfile();
        }

        var plan = _planBuilder.BuildPlan(
            new OccupationAnchorPose(engagePosition, facingDirection),
            profile.ApproachDistance,
            profile.NavigationStopDistance,
            profile.EngageRadius,
            profile.SustainRadius);

        target = new OccupationTarget(
            targetRef,
            craftStationData.DisplayName,
            craftStationData.BuildingId,
            craftStationData.ZoneId,
            plan,
            OccupationExecutionProfile.CraftStation(craftStationData.Interactable));
        return true;
    }
}
