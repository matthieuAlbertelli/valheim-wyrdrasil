using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AssignedSlotOccupationResolver : IOccupationResolver
{
    private readonly ZoneSlotService _slotService;

    public AssignedSlotOccupationResolver(ZoneSlotService slotService)
    {
        _slotService = slotService;
    }

    public ResidentRoutineActivityType ActivityType => ResidentRoutineActivityType.WorkAtAssignedSlot;

    public bool TryResolve(RegisteredNpcData resident, out OccupationTarget target)
    {
        if (!resident.AssignedSlotId.HasValue ||
            !_slotService.TryGetSlotById(resident.AssignedSlotId.Value, out var slotData))
        {
            target = null!;
            return false;
        }

        target = new OccupationTarget(
            new OccupationTargetRef(OccupationTargetKind.Slot, slotData.Id),
            $"Slot #{slotData.Id}",
            slotData.BuildingId,
            slotData.ZoneId,
            new OccupationAnchor(slotData.Position, slotData.Position, default),
            OccupationUseMode.Stand);

        return true;
    }

    public void Release(RegisteredNpcData resident)
    {
    }
}
