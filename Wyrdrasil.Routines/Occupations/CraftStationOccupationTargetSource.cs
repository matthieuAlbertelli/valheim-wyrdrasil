using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationTargetSource : IOccupationTargetSource
{
    private readonly CraftStationService _craftStationService;

    public CraftStationOccupationTargetSource(CraftStationService craftStationService)
    {
        _craftStationService = craftStationService;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.CraftStation;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (!_craftStationService.TryGetCraftStationById(targetRef.TargetId, out var craftStationData))
        {
            target = null!;
            return false;
        }

        target = new OccupationTarget(
            targetRef,
            craftStationData.DisplayName,
            craftStationData.BuildingId,
            craftStationData.ZoneId,
            new OccupationAnchor(craftStationData.ApproachPosition, craftStationData.UsePosition, craftStationData.UseForward),
            OccupationExecutionProfile.CraftStation(craftStationData.Interactable));
        return true;
    }
}
