using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class SlotOccupationTargetSource : IOccupationTargetSource
{
    private readonly ZoneSlotService _slotService;
    private readonly OccupationTargetFactory _targetFactory;

    public SlotOccupationTargetSource(ZoneSlotService slotService, OccupationTargetFactory targetFactory)
    {
        _slotService = slotService;
        _targetFactory = targetFactory;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.Slot;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (targetRef.TargetKind != TargetKind ||
            !_slotService.TryGetSlotById(targetRef.TargetId, out var slotData))
        {
            target = null!;
            return false;
        }

        var anchorDefinition = OccupationAnchorDefinition.FromExplicitPositions(
            OccupationAnchorAttachmentKind.StandingPoint,
            slotData.Position,
            slotData.Position,
            slotData.FacingDirection,
            navigationStopDistance: 0.30f,
            engageRadius: 0.40f,
            sustainRadius: 0.75f,
            approachProfile: OccupationAnchorApproachProfile.StandingPointDefault);

        target = _targetFactory.CreateAnchoredTarget(
            new OccupationTargetRef(TargetKind, slotData.Id),
            $"Slot #{slotData.Id}",
            slotData.BuildingId,
            slotData.ZoneId,
            anchorDefinition,
            OccupationExecutionProfile.Stand(anchorDefinition));

        return true;
    }
}
