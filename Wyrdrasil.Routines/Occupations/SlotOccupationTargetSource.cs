using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Routines.Occupations;

public sealed class SlotOccupationTargetSource : IOccupationTargetSource
{
    private readonly ZoneSlotService _slotService;

    public SlotOccupationTargetSource(ZoneSlotService slotService)
    {
        _slotService = slotService;
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

        var plan = new OccupationPosePlan(
            slotData.Position,
            slotData.Position,
            slotData.FacingDirection,
            0.30f,
            0.40f,
            0.75f);

        target = new OccupationTarget(
            new OccupationTargetRef(TargetKind, slotData.Id),
            $"Slot #{slotData.Id}",
            slotData.BuildingId,
            slotData.ZoneId,
            plan,
            OccupationExecutionProfile.Stand());

        return true;
    }
}
