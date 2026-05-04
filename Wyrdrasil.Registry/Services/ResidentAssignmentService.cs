using System.Linq;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Runtime;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class ResidentAssignmentService
{
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly ISoulsRuntimeApi _soulsRuntimeApi;
    private readonly IRoutinesRuntimeApi _routinesRuntimeApi;

    private readonly ResidentVisualService _visualService;

    public ResidentAssignmentService(
        ISettlementsRuntimeApi settlementsRuntimeApi,
        IConstructionRuntimeApi constructionRuntimeApi,
        ISoulsRuntimeApi soulsRuntimeApi,
        IRoutinesRuntimeApi routinesRuntimeApi,
        ResidentVisualService visualService)
    {
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _constructionRuntimeApi = constructionRuntimeApi;
        _soulsRuntimeApi = soulsRuntimeApi;
        _routinesRuntimeApi = routinesRuntimeApi;
        _visualService = visualService;
    }

    public bool TryClearSlotAssignment(ZoneSlotData slotData, out RegisteredNpcData? resident)
    {
        resident = null;
        if (!_settlementsRuntimeApi.TryClearSlotAssignment(slotData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            return false;
        }

        if (_soulsRuntimeApi.TryGetResidentById(previousResidentId.Value, out resident))
        {
            _routinesRuntimeApi.ReleaseOccupation(resident, false);
            ClearWorkAssignment(resident, clearRole: true);
            _visualService.UpdateMarker(resident);
        }

        return true;
    }

    public bool TryClearSeatAssignment(RegisteredSeatData seatData, out RegisteredNpcData? resident)
    {
        resident = null;
        if (!_settlementsRuntimeApi.TryClearSeatAssignment(seatData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            return false;
        }

        if (_soulsRuntimeApi.TryGetResidentById(previousResidentId.Value, out resident))
        {
            _routinesRuntimeApi.ReleaseOccupation(resident);
            ClearMealAssignment(resident);
            _visualService.UpdateMarker(resident);
        }

        return true;
    }

    public bool TryClearBedAssignment(RegisteredBedData bedData, out RegisteredNpcData? resident)
    {
        resident = null;
        if (!_settlementsRuntimeApi.TryClearBedAssignment(bedData.Id, out var previousResidentId) || !previousResidentId.HasValue)
        {
            return false;
        }

        if (_soulsRuntimeApi.TryGetResidentById(previousResidentId.Value, out resident))
        {
            _routinesRuntimeApi.ReleaseOccupation(resident);
            ClearSleepAssignment(resident);
            _visualService.UpdateMarker(resident);
        }

        return true;
    }

    public bool TryAssignInnkeeperRole(RegisteredNpcData resident, Character targetCharacter, out ZoneSlotData? slotData)
    {
        DetachIfAttached(targetCharacter);
        _settlementsRuntimeApi.ClearSlotAssignmentForResident(resident.Id);
        _settlementsRuntimeApi.ClearCraftStationAssignmentForResident(resident.Id);
        _settlementsRuntimeApi.ClearSeatAssignmentForResident(resident.Id);
        ClearMealAssignment(resident);
        ClearWorkAssignment(resident, clearRole: false);
        _routinesRuntimeApi.EnsureDefaultAutonomySchedules(resident);

        if (!_settlementsRuntimeApi.TryAssignInnkeeperSlot(resident.Id, out slotData) || slotData == null)
        {
            return false;
        }

        resident.SetRole(NpcRole.Innkeeper);
        resident.SetAssignment(ResidentAssignmentPurpose.Work, new OccupationTargetRef(OccupationTargetKind.Slot, slotData.Id));
        _routinesRuntimeApi.ApplyDefaultInnkeeperSchedule(resident);
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
        _settlementsRuntimeApi.ClearBedAssignmentForResident(resident.Id);
        ClearSleepAssignment(resident);

        if (!_settlementsRuntimeApi.TryAssignBed(resident.Id, out bedData) || bedData == null)
        {
            return false;
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Sleep, new OccupationTargetRef(OccupationTargetKind.Bed, bedData.Id));
        _routinesRuntimeApi.ApplyDefaultBedSleepSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToSlot(RegisteredNpcData resident, ZoneSlotData slotData)
    {
        if (resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Work, OccupationTargetKind.Slot, out var slotId) && slotId == slotData.Id)
        {
            return true;
        }

        _settlementsRuntimeApi.ClearSlotAssignmentForResident(resident.Id);
        _settlementsRuntimeApi.ClearCraftStationAssignmentForResident(resident.Id);
        _settlementsRuntimeApi.ClearSeatAssignmentForResident(resident.Id);
        ClearMealAssignment(resident);
        ClearWorkAssignment(resident, clearRole: true);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_settlementsRuntimeApi.ForceAssignInnkeeperSlot(slotData.Id, resident.Id, out var previousResidentId, out var resolvedSlot) || resolvedSlot == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _soulsRuntimeApi.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            ClearWorkAssignment(displacedResident, clearRole: true);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetRole(NpcRole.Innkeeper);
        resident.SetAssignment(ResidentAssignmentPurpose.Work, new OccupationTargetRef(OccupationTargetKind.Slot, resolvedSlot.Id));
        _routinesRuntimeApi.ApplyDefaultInnkeeperSchedule(resident);
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

        _settlementsRuntimeApi.ClearSeatAssignmentForResident(resident.Id);
        ClearMealAssignment(resident);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_settlementsRuntimeApi.ForceAssignSeat(seatData.Id, resident.Id, out var previousResidentId, out var resolvedSeat) || resolvedSeat == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _soulsRuntimeApi.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _routinesRuntimeApi.ReleaseOccupation(displacedResident);
            ClearMealAssignment(displacedResident);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Meal, new OccupationTargetRef(OccupationTargetKind.Seat, resolvedSeat.Id));
        _routinesRuntimeApi.ApplyDefaultSeatMealSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToBed(RegisteredNpcData resident, RegisteredBedData bedData)
    {
        if (resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Sleep, OccupationTargetKind.Bed, out var bedId) && bedId == bedData.Id)
        {
            return true;
        }

        _settlementsRuntimeApi.ClearBedAssignmentForResident(resident.Id);
        ClearSleepAssignment(resident);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_settlementsRuntimeApi.ForceAssignBed(bedData.Id, resident.Id, out var previousResidentId, out var resolvedBed) || resolvedBed == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _soulsRuntimeApi.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _routinesRuntimeApi.ReleaseOccupation(displacedResident);
            ClearSleepAssignment(displacedResident);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Sleep, new OccupationTargetRef(OccupationTargetKind.Bed, resolvedBed.Id));
        _routinesRuntimeApi.ApplyDefaultBedSleepSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryForceAssignToCraftStation(RegisteredNpcData resident, RegisteredCraftStationData craftStationData)
    {
        if (_constructionRuntimeApi.TryGetProjectIdByCraftStation(craftStationData.Id, out var reservedProjectId))
        {
            return false;
        }

        if (resident.TryGetAssignedTargetId(ResidentAssignmentPurpose.Work, OccupationTargetKind.CraftStation, out var craftStationId) && craftStationId == craftStationData.Id)
        {
            return true;
        }

        _settlementsRuntimeApi.ClearSlotAssignmentForResident(resident.Id);
        _settlementsRuntimeApi.ClearCraftStationAssignmentForResident(resident.Id);
        ClearWorkAssignment(resident, clearRole: true);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_settlementsRuntimeApi.ForceAssignCraftStation(craftStationData.Id, resident.Id, out var previousResidentId, out var resolvedCraftStation) || resolvedCraftStation == null)
        {
            return false;
        }

        if (previousResidentId.HasValue && previousResidentId.Value != resident.Id && _soulsRuntimeApi.TryGetResidentById(previousResidentId.Value, out var displacedResident))
        {
            _routinesRuntimeApi.ReleaseOccupation(displacedResident);
            ClearWorkAssignment(displacedResident, clearRole: false);
            _visualService.UpdateMarker(displacedResident);
        }

        resident.SetAssignment(ResidentAssignmentPurpose.Work, new OccupationTargetRef(OccupationTargetKind.CraftStation, resolvedCraftStation.Id));
        _routinesRuntimeApi.ApplyDefaultCraftStationWorkSchedule(resident);
        _visualService.UpdateMarker(resident);
        return true;
    }

    public bool TryAssignToConstructionProject(RegisteredNpcData resident, int projectId, out int workPostId, out string failureReason)
    {
        _settlementsRuntimeApi.ClearSlotAssignmentForResident(resident.Id);
        _settlementsRuntimeApi.ClearCraftStationAssignmentForResident(resident.Id);
        _routinesRuntimeApi.ReleaseOccupation(resident, detachIfAttached: false);
        ClearWorkAssignment(resident, clearRole: true, clearConstructionRuntime: false);
        _visualService.UpdateMarker(resident);
        DetachResidentIfBound(resident);

        if (!_constructionRuntimeApi.TryAssignResidentToProject(resident.Id, projectId, out var workPost, out failureReason))
        {
            workPostId = 0;
            return false;
        }

        resident.AssignConstructionWorkPost(workPost.Id);
        _routinesRuntimeApi.ApplyDefaultConstructionWorkSchedule(resident);
        _visualService.UpdateMarker(resident);
        workPostId = workPost.Id;
        return true;
    }

    public bool TryClearConstructionAssignment(RegisteredNpcData resident, out int projectId, out int workPostId, out string failureReason)
    {
        _routinesRuntimeApi.ReleaseOccupation(resident, detachIfAttached: false);

        if (!_constructionRuntimeApi.TryClearResidentAssignment(resident.Id, out projectId, out workPostId))
        {
            failureReason = $"Resident #{resident.Id} has no construction work assignment.";
            return false;
        }

        ClearWorkAssignment(resident, clearRole: false);
        _visualService.UpdateMarker(resident);
        failureReason = string.Empty;
        return true;
    }

    public void HandleDeletedSlot(int slotId)
    {
        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Work, OccupationTargetKind.Slot, slotId)))
        {
            ClearWorkAssignment(resident, clearRole: true);
            _visualService.UpdateMarker(resident);
        }
    }

    public void HandleDeletedSeat(int seatId)
    {
        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Meal, OccupationTargetKind.Seat, seatId)))
        {
            _routinesRuntimeApi.ReleaseOccupation(resident);
            ClearMealAssignment(resident);
            _visualService.UpdateMarker(resident);
        }
    }

    public void HandleDeletedBed(int bedId)
    {
        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Sleep, OccupationTargetKind.Bed, bedId)))
        {
            _routinesRuntimeApi.ReleaseOccupation(resident);
            ClearSleepAssignment(resident);
            _visualService.UpdateMarker(resident);
        }
    }

    public int ClearStaleConstructionAssignments()
    {
        var clearedCount = 0;
        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs.Where(candidate => candidate.AssignedConstructionWorkPostId.HasValue))
        {
            var workPostId = resident.AssignedConstructionWorkPostId!.Value;
            if (_constructionRuntimeApi.TryGetWorkPost(workPostId, out _))
            {
                continue;
            }

            _routinesRuntimeApi.ReleaseOccupation(resident, detachIfAttached: false);
            ClearWorkAssignment(resident, clearRole: false, clearConstructionRuntime: false);
            _visualService.UpdateMarker(resident);
            clearedCount++;
        }

        return clearedCount;
    }

    public void HandleDeletedCraftStation(int craftStationId)
    {
        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Work, OccupationTargetKind.CraftStation, craftStationId)))
        {
            _routinesRuntimeApi.ReleaseOccupation(resident);
            ClearWorkAssignment(resident, clearRole: false);
            _visualService.UpdateMarker(resident);
        }

        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs.Where(candidate =>
                     HasAssignmentTarget(candidate, ResidentAssignmentPurpose.Work, OccupationTargetKind.ConstructionWorkPost, candidate.AssignedConstructionWorkPostId ?? 0)))
        {
            if (!resident.AssignedConstructionWorkPostId.HasValue ||
                !_constructionRuntimeApi.TryGetWorkPost(resident.AssignedConstructionWorkPostId.Value, out var workPost) ||
                workPost.CraftStationId != craftStationId)
            {
                continue;
            }

            _routinesRuntimeApi.ReleaseOccupation(resident, detachIfAttached: false);
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

    private void ClearWorkAssignment(RegisteredNpcData resident, bool clearRole, bool clearConstructionRuntime = true)
    {
        resident.ClearAssignment(ResidentAssignmentPurpose.Work);
        _routinesRuntimeApi.ClearSlotSchedule(resident);
        _routinesRuntimeApi.ClearCraftStationSchedule(resident);
        _routinesRuntimeApi.ClearConstructionWorkSchedule(resident);
        if (clearConstructionRuntime)
        {
            _constructionRuntimeApi.TryClearResidentAssignment(resident.Id, out _, out _);
        }

        if (clearRole && resident.Role == NpcRole.Innkeeper)
        {
            resident.SetRole(NpcRole.Villager);
        }
    }

    private void ClearMealAssignment(RegisteredNpcData resident)
    {
        resident.ClearAssignment(ResidentAssignmentPurpose.Meal);
        _routinesRuntimeApi.ClearAssignedSeatSchedule(resident);
    }

    private void ClearSleepAssignment(RegisteredNpcData resident)
    {
        resident.ClearAssignment(ResidentAssignmentPurpose.Sleep);
        _routinesRuntimeApi.ClearBedSchedule(resident);
    }

    private void DetachResidentIfBound(RegisteredNpcData resident)
    {
        if (_soulsRuntimeApi.TryGetBoundCharacter(resident.Id, out var character))
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
