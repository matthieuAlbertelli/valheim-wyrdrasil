using System.Linq;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentAssignmentService
{
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryBedService _bedService;
    private readonly RegistryResidentRuntimeService _runtimeService;
    private readonly RegistryResidentScheduleService _scheduleService;
    private readonly RegistryResidentOccupationService _occupationService;
    private readonly RegistryResidentCatalogService _catalogService;
    private readonly RegistryResidentVisualService _visualService;

    public RegistryResidentAssignmentService(
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryBedService bedService,
        RegistryResidentRuntimeService runtimeService,
        RegistryResidentScheduleService scheduleService,
        RegistryResidentOccupationService occupationService,
        RegistryResidentCatalogService catalogService,
        RegistryResidentVisualService visualService)
    {
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
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
            resident.ClearAssignedSlot();
            resident.SetRole(NpcRole.Villager);
            _scheduleService.ClearSlotSchedule(resident);
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
            resident.ClearAssignedSeat();
            _scheduleService.ClearSeatSchedule(resident);
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
            resident.ClearAssignedBed();
            _scheduleService.ClearBedSchedule(resident);
            _visualService.UpdateMarker(resident);
        }

        return true;
    }

    public bool TryAssignInnkeeperRole(RegisteredNpcData resident, Character targetCharacter, out ZoneSlotData? slotData)
    {
        DetachIfAttached(targetCharacter);
        _slotService.ClearAssignmentForResident(resident.Id);
        _seatService.ClearAssignmentForResident(resident.Id);
        resident.ClearAssignedSeat();
        _scheduleService.ClearSeatSchedule(resident);

        if (!_slotService.TryAssignInnkeeperSlot(resident.Id, out slotData) || slotData == null)
        {
            return false;
        }

        resident.SetRole(NpcRole.Innkeeper);
        resident.AssignSlot(slotData.Id);
        _scheduleService.ApplyDefaultInnkeeperSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryAssignSeat(RegisteredNpcData resident, Character targetCharacter, out RegisteredSeatData? seatData)
    {
        DetachIfAttached(targetCharacter);
        _seatService.ClearAssignmentForResident(resident.Id);
        resident.ClearAssignedSeat();

        if (!_seatService.TryAssignSeat(resident.Id, out seatData) || seatData == null)
        {
            return false;
        }

        resident.SetRole(NpcRole.Villager);
        resident.AssignSeat(seatData.Id);
        _scheduleService.ApplyDefaultSeatMealSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryAssignBed(RegisteredNpcData resident, Character targetCharacter, out RegisteredBedData? bedData)
    {
        DetachIfAttached(targetCharacter);
        _bedService.ClearAssignmentForResident(resident.Id);
        resident.ClearAssignedBed();

        if (!_bedService.TryAssignBed(resident.Id, out bedData) || bedData == null)
        {
            return false;
        }

        resident.AssignBed(bedData.Id);
        _scheduleService.ApplyDefaultBedSleepSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToSlot(RegisteredNpcData resident, ZoneSlotData slotData)
    {
        if (resident.AssignedSlotId == slotData.Id)
        {
            return true;
        }

        _slotService.ClearAssignmentForResident(resident.Id);
        _seatService.ClearAssignmentForResident(resident.Id);
        resident.ClearAssignedSeat();
        resident.ClearAssignedSlot();
        resident.SetRole(NpcRole.Villager);
        _scheduleService.ClearSeatSchedule(resident);
        _scheduleService.ClearSlotSchedule(resident);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_slotService.ForceAssignInnkeeperSlot(slotData.Id, resident.Id, out var previousResidentId, out var resolvedSlot) || resolvedSlot == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _catalogService.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            displacedResident.ClearAssignedSlot();
            displacedResident.SetRole(NpcRole.Villager);
            _scheduleService.ClearSlotSchedule(displacedResident);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetRole(NpcRole.Innkeeper);
        resident.AssignSlot(resolvedSlot.Id);
        _scheduleService.ApplyDefaultInnkeeperSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToSeat(RegisteredNpcData resident, RegisteredSeatData seatData)
    {
        if (resident.AssignedSeatId == seatData.Id)
        {
            return true;
        }

        _seatService.ClearAssignmentForResident(resident.Id);
        resident.ClearAssignedSeat();
        resident.SetRole(NpcRole.Villager);
        _scheduleService.ClearSeatSchedule(resident);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_seatService.ForceAssignSeat(seatData.Id, resident.Id, out var previousResidentId, out var resolvedSeat) || resolvedSeat == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _catalogService.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _occupationService.ReleaseOccupation(displacedResident);
            displacedResident.ClearAssignedSeat();
            _scheduleService.ClearSeatSchedule(displacedResident);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.AssignSeat(resolvedSeat.Id);
        _scheduleService.ApplyDefaultSeatMealSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToBed(RegisteredNpcData resident, RegisteredBedData bedData)
    {
        if (resident.AssignedBedId == bedData.Id)
        {
            return true;
        }

        _bedService.ClearAssignmentForResident(resident.Id);
        resident.ClearAssignedBed();
        _scheduleService.ClearBedSchedule(resident);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_bedService.ForceAssignBed(bedData.Id, resident.Id, out var previousResidentId, out var resolvedBed) || resolvedBed == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _catalogService.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _occupationService.ReleaseOccupation(displacedResident);
            displacedResident.ClearAssignedBed();
            _scheduleService.ClearBedSchedule(displacedResident);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.AssignBed(resolvedBed.Id);
        _scheduleService.ApplyDefaultBedSleepSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public void HandleDeletedSlot(int slotId)
    {
        foreach (var resident in _catalogService.RegisteredNpcs.Where(candidate => candidate.AssignedSlotId == slotId))
        {
            resident.ClearAssignedSlot();
            resident.SetRole(NpcRole.Villager);
            _scheduleService.ClearSlotSchedule(resident);
            _visualService.UpdateMarker(resident);
        }
    }

    public void HandleDeletedSeat(int seatId)
    {
        foreach (var resident in _catalogService.RegisteredNpcs.Where(candidate => candidate.AssignedSeatId == seatId))
        {
            _occupationService.ReleaseOccupation(resident);
            resident.ClearAssignedSeat();
            _scheduleService.ClearSeatSchedule(resident);
            _visualService.UpdateMarker(resident);
        }
    }

    public void HandleDeletedBed(int bedId)
    {
        foreach (var resident in _catalogService.RegisteredNpcs.Where(candidate => candidate.AssignedBedId == bedId))
        {
            _occupationService.ReleaseOccupation(resident);
            resident.ClearAssignedBed();
            _scheduleService.ClearBedSchedule(resident);
            _visualService.UpdateMarker(resident);
        }
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
