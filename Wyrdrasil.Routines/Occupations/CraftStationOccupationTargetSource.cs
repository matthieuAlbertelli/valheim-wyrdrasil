using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationTargetSource : IOccupationTargetSource
{
    private readonly CraftStationService _craftStationService;
    private readonly CraftStationOccupationPlanBuilder _planBuilder;

    public CraftStationOccupationTargetSource(CraftStationService craftStationService, CraftStationOccupationPlanBuilder planBuilder)
    {
        _craftStationService = craftStationService;
        _planBuilder = planBuilder;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.CraftStation;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (!_craftStationService.TryGetCraftStationById(targetRef.TargetId, out var craftStationData) ||
            !_planBuilder.TryBuildPlan(craftStationData, out var plan))
        {
            target = null!;
            return false;
        }

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
