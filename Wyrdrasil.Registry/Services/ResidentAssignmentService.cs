using System.Linq;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class ResidentAssignmentService
{
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly ResidentScheduleService _scheduleService;
    private readonly ResidentOccupationService _occupationService;
    private readonly ResidentCatalogService _catalogService;
    private readonly ResidentVisualService _visualService;

    public ResidentAssignmentService(
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        ResidentRuntimeService runtimeService,
        ResidentScheduleService scheduleService,
        ResidentOccupationService occupationService,
        ResidentCatalogService catalogService,
        ResidentVisualService visualService)
    {
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _runtimeService = runtimeService;
        _scheduleService = scheduleService;
        _occupationService = occupationService;
        _catalogService = catalogService;
        _visualService = visualService;
    }

    public bool TryClearSlotAssignment(ZoneSlotData slotData, out RegisteredNpcData? resident)
    {
        resident = null;
        if (!_slotService.ClearSlotAssignment(slotData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            return false;
        }

        if (_catalogService.TryGetResidentById(previousResidentId.Value, out resident))
        {
            _occupationService.ReleaseOccupation(resident, false);
            ClearWorkAssignment(resident, clearRole: true);
            _visualService.UpdateMarker(resident);
        }

        return true;
    }

    public bool TryClearSeatAssignment(RegisteredSeatData seatData, out RegisteredNpcData? resident)
    {
        resident = null;
        if (!_seatService.ClearSeatAssignment(seatData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            return false;
        }

        if (_catalogService.TryGetResidentById(previousResidentId.Value, out resident))
        {
            _occupationService.ReleaseOccupation(resident);
            ClearMealAssignment(resident);
            _visualService.UpdateMarker(resident);
        }

        return true;
    }

    public bool TryClearBedAssignment(RegisteredBedData bedData, out RegisteredNpcData? resident)
    {
        resident = null;
        if (!_bedService.ClearBedAssignment(bedData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            return false;
        }

        if (_catalogService.TryGetResidentById(previousResidentId.Value, out resident))
        {
            _occupationService.ReleaseOccupation(resident);
            ClearSleepAssignment(resident);
            _visualService.UpdateMarker(resident);
        }

        return true;
    }

    public bool TryAssignInnkeeperRole(RegisteredNpcData resident, Character targetCharacter, out ZoneSlotData? slotData)
    {
        DetachIfAttached(targetCharacter);
        _slotService.ClearAssignmentForResident(resident.Id);
        _craftStationService.ClearAssignmentForResident(resident.Id);
        _seatService.ClearAssignmentForResident(resident.Id);
        ClearMealAssignment(resident);
        ClearWorkAssignment(resident, clearRole: false);
        _scheduleService.EnsureDefaultAutonomySchedules(resident);

        if (!_slotService.TryAssignInnkeeperSlot(resident.Id, out slotData) || slotData == null)
        {
            return false;
        }

        resident.SetRole(NpcRole.Innkeeper);
        resident.SetAssignment(ResidentAssignmentPurpose.Work, new OccupationTargetRef(OccupationTargetKind.Slot, slotData.Id));
        _scheduleService.ApplyDefaultInnkeeperSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryAssignSeat(RegisteredNpcData resident, Character targetCharacter, out RegisteredSeatData? seatData)
    {
        seatData = null;
        return false;
    }

    public bool TryAssignBed(RegisteredNpcData resident, Character targetCharacter, out RegisteredBedData? bedData)
    {
        DetachIfAttached(targetCharacter);
        _bedService.ClearAssignmentForResident(resident.Id);
        ClearSleepAssignment(resident);

        if (!_bedService.TryAssignBed(resident.Id, out bedData) || bedData == null)
        {
            return false;
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Sleep, new OccupationTargetRef(OccupationTargetKind.Bed, bedData.Id));
        _scheduleService.ApplyDefaultBedSleepSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToSlot(RegisteredNpcData resident, ZoneSlotData slotData)
    {
        if (resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Work, OccupationTargetKind.Slot, out var slotId) && slotId == slotData.Id)
        {
            return true;
        }

        _slotService.ClearAssignmentForResident(resident.Id);
        _craftStationService.ClearAssignmentForResident(resident.Id);
        _seatService.ClearAssignmentForResident(resident.Id);
        ClearMealAssignment(resident);
        ClearWorkAssignment(resident, clearRole: true);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_slotService.ForceAssignInnkeeperSlot(slotData.Id, resident.Id, out var previousResidentId, out var resolvedSlot) || resolvedSlot == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _catalogService.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            ClearWorkAssignment(displacedResident, clearRole: true);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetRole(NpcRole.Innkeeper);
        resident.SetAssignment(ResidentAssignmentPurpose.Work, new OccupationTargetRef(OccupationTargetKind.Slot, resolvedSlot.Id));
        _scheduleService.ApplyDefaultInnkeeperSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToSeat(RegisteredNpcData resident, RegisteredSeatData seatData)
    {
        if (seatData.UsageType != SeatUsageType.Reserved)
        {
            return false;
        }

        if (resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Meal, OccupationTargetKind.Seat, out var seatId) && seatId == seatData.Id)
        {
            return true;
        }

        _seatService.ClearAssignmentForResident(resident.Id);
        ClearMealAssignment(resident);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_seatService.ForceAssignSeat(seatData.Id, resident.Id, out var previousResidentId, out var resolvedSeat) || resolvedSeat == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _catalogService.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _occupationService.ReleaseOccupation(displacedResident);
            ClearMealAssignment(displacedResident);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Meal, new OccupationTargetRef(OccupationTargetKind.Seat, resolvedSeat.Id));
        _scheduleService.ApplyDefaultSeatMealSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToBed(RegisteredNpcData resident, RegisteredBedData bedData)
    {
        if (resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Sleep, OccupationTargetKind.Bed, out var bedId) && bedId == bedData.Id)
        {
            return true;
        }

        _bedService.ClearAssignmentForResident(resident.Id);
        ClearSleepAssignment(resident);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_bedService.ForceAssignBed(bedData.Id, resident.Id, out var previousResidentId, out var resolvedBed) || resolvedBed == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _catalogService.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _occupationService.ReleaseOccupation(displacedResident);
            ClearSleepAssignment(displacedResident);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Sleep, new OccupationTargetRef(OccupationTargetKind.Bed, resolvedBed.Id));
        _scheduleService.ApplyDefaultBedSleepSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToCraftStation(RegisteredNpcData resident, RegisteredCraftStationData craftStationData)
    {
        if (resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Work, OccupationTargetKind.CraftStation, out var craftStationId) && craftStationId == craftStationData.Id)
        {
            return true;
        }

        _slotService.ClearAssignmentForResident(resident.Id);
        _craftStationService.ClearAssignmentForResident(resident.Id);
        ClearWorkAssignment(resident, clearRole: true);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_craftStationService.ForceAssignCraftStation(craftStationData.Id, resident.Id, out var previousResidentId, out var resolvedCraftStation) || resolvedCraftStation == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _catalogService.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _occupationService.ReleaseOccupation(displacedResident);
            ClearWorkAssignment(displacedResident, clearRole: false);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Work, new OccupationTargetRef(OccupationTargetKind.CraftStation, resolvedCraftStation.Id));
        _scheduleService.ApplyDefaultCraftStationWorkSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public void HandleDeletedSlot(int slotId)
    {
        foreach (var resident in _catalogService.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Work, OccupationTargetKind.Slot, slotId)))
        {
            ClearWorkAssignment(resident, clearRole: true);
            _visualService.UpdateMarker(resident);
        }
    }

    public void HandleDeletedSeat(int seatId)
    {
        foreach (var resident in _catalogService.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Meal, OccupationTargetKind.Seat, seatId)))
        {
            _occupationService.ReleaseOccupation(resident);
            ClearMealAssignment(resident);
            _visualService.UpdateMarker(resident);
        }
    }

    public void HandleDeletedBed(int bedId)
    {
        foreach (var resident in _catalogService.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Sleep, OccupationTargetKind.Bed, bedId)))
        {
            _occupationService.ReleaseOccupation(resident);
            ClearSleepAssignment(resident);
            _visualService.UpdateMarker(resident);
        }
    }

    public void HandleDeletedCraftStation(int craftStationId)
    {
        foreach (var resident in _catalogService.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Work, OccupationTargetKind.CraftStation, craftStationId)))
        {
            _occupationService.ReleaseOccupation(resident);
            ClearWorkAssignment(resident, clearRole: false);
            _visualService.UpdateMarker(resident);
        }
    }

    private static bool HasAssignmentTarget(
        RegisteredNpcData resident,
        ResidentAssignmentPurpose purpose,
        OccupationTargetKind targetKind,
        int targetId)
    {
        return resident.TryGetAssignedTargetId(purpose, targetKind, out var assignedTargetId) && assignedTargetId == targetId;
    }

    private void ClearWorkAssignment(RegisteredNpcData resident, bool clearRole)
    {
        resident.ClearAssignment(ResidentAssignmentPurpose.Work);
        _scheduleService.ClearSlotSchedule(resident);
        _scheduleService.ClearCraftStationSchedule(resident);
        if (clearRole && resident.Role == NpcRole.Innkeeper)
        {
            resident.SetRole(NpcRole.Villager);
        }
    }

    private void ClearMealAssignment(RegisteredNpcData resident)
    {
        resident.ClearAssignment(ResidentAssignmentPurpose.Meal);
        _scheduleService.ClearAssignedSeatSchedule(resident);
    }

    private void ClearSleepAssignment(RegisteredNpcData resident)
    {
        resident.ClearAssignment(ResidentAssignmentPurpose.Sleep);
        _scheduleService.ClearBedSchedule(resident);
    }

    private void DetachResidentIfBound(RegisteredNpcData resident)
    {
        if (_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            DetachIfAttached(character);
        }
    }

    private static void DetachIfAttached(Character character)
    {
        if (character is Humanoid humanoid && humanoid.IsAttached())
        {
            humanoid.AttachStop();
        }
    }
}
