using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationTargetSource : IOccupationTargetSource
{
    private readonly CraftStationService _craftStationService;
    private readonly OccupationTargetFactory _targetFactory;

    public CraftStationOccupationTargetSource(CraftStationService craftStationService, OccupationTargetFactory targetFactory)
    {
        _craftStationService = craftStationService;
        _targetFactory = targetFactory;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.CraftStation;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (targetRef.TargetKind != TargetKind ||
            !_craftStationService.TryGetCraftStationById(targetRef.TargetId, out var craftStationData) ||
            !craftStationData.TryResolveWorldAnchor(out var engagePosition, out var facingDirection))
        {
            target = null!;
            return false;
        }

        if (!CraftStationInteractionProfileRegistry.TryGetProfileById(craftStationData.InteractionProfileId, out var profile))
        {
            profile = CraftStationInteractionProfileRegistry.GetDefaultProfile();
        }

        var anchorDefinition = OccupationAnchorDefinition.FromPose(
            OccupationAnchorAttachmentKind.WorkPoint,
            engagePosition,
            facingDirection,
            profile.ApproachDistance,
            profile.NavigationStopDistance,
            profile.EngageRadius,
            profile.SustainRadius,
            approachProfile: OccupationAnchorApproachProfile.WorkPointDefault);

        target = _targetFactory.CreateAnchoredTarget(
            targetRef,
            craftStationData.DisplayName,
            craftStationData.BuildingId,
            craftStationData.ZoneId,
            anchorDefinition,
            OccupationExecutionProfile.CraftStation(craftStationData.Interactable, anchorDefinition));
        return true;
    }
}
