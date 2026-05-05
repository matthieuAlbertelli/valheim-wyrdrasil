using UnityEngine;
using Wyrdrasil.Core.Tool;
using System.Collections.Generic;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Runtime;

public sealed class SettlementsRuntimeApi : ISettlementsRuntimeApi
{
    private readonly BuildingService _buildingService;
    private readonly FunctionalZoneService _zoneService;
    private readonly NavigationWaypointService _waypointService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly FunctionalZoneRuntimeService _zoneRuntimeService;

    public SettlementsRuntimeApi(
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        FunctionalZoneRuntimeService zoneRuntimeService)
    {
        _buildingService = buildingService;
        _zoneService = zoneService;
        _waypointService = waypointService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _zoneRuntimeService = zoneRuntimeService;
    }

    public IReadOnlyList<FunctionalZoneData> Zones => _zoneService.Zones;
    public IReadOnlyList<NavigationWaypointData> Waypoints => _waypointService.Waypoints;
    public int? PendingLinkStartWaypointId => _waypointService.PendingLinkStartWaypointId;
    public IReadOnlyList<ZoneSlotData> Slots => _slotService.Slots;
    public IReadOnlyList<RegisteredSeatData> Seats => _seatService.Seats;
    public IReadOnlyList<RegisteredBedData> Beds => _bedService.Beds;
    public IReadOnlyList<RegisteredCraftStationData> CraftStations => _craftStationService.CraftStations;
    public bool TryGetRuntimeState(int zoneId, out FunctionalZoneRuntimeState runtimeState) => _zoneRuntimeService.TryGetRuntimeState(zoneId, out runtimeState);
    public FunctionalZoneRuntimeState Evaluate(FunctionalZoneData zone) => _zoneRuntimeService.Evaluate(zone);
    public bool IsSeatEligibleForPublicSocialUse(RegisteredSeatData seat) => _zoneRuntimeService.IsSeatEligibleForPublicSocialUse(seat);
    public bool TryGetSeatById(int seatId, out RegisteredSeatData seatData) => _seatService.TryGetSeatById(seatId, out seatData);
    public bool TryGetOccupiedSeatForResident(int residentId, out RegisteredSeatData seatData) => _seatService.TryGetOccupiedSeatForResident(residentId, out seatData);
    public bool TryGetCraftStationById(int craftStationId, out RegisteredCraftStationData craftStationData) => _craftStationService.TryGetCraftStationById(craftStationId, out craftStationData);
    public bool TryResolveCraftStationAnchor(int craftStationId, out Vector3 anchorWorldPosition, out Vector3 anchorWorldForward)
    {
        if (_craftStationService.TryGetCraftStationById(craftStationId, out var craftStationData) &&
            craftStationData.TryResolveWorldAnchor(out anchorWorldPosition, out anchorWorldForward))
        {
            return true;
        }

        anchorWorldPosition = Vector3.zero;
        anchorWorldForward = Vector3.forward;
        return false;
    }

    public bool TryGetCraftStationInteractionProfile(int craftStationId, out CraftStationInteractionProfile profile)
    {
        if (_craftStationService.TryGetCraftStationById(craftStationId, out var craftStationData))
        {
            return _craftStationService.TryGetInteractionProfile(craftStationData, out profile);
        }

        profile = CraftStationInteractionProfileRegistry.GetDefaultProfile();
        return false;
    }
    public bool TryClearSlotAssignment(int slotId, out int? previousResidentId) => _slotService.ClearSlotAssignment(slotId, out previousResidentId);
    public bool TryClearSeatAssignment(int seatId, out int? previousResidentId) => _seatService.ClearSeatAssignment(seatId, out previousResidentId);
    public bool TryClearBedAssignment(int bedId, out int? previousResidentId) => _bedService.ClearBedAssignment(bedId, out previousResidentId);
    public bool TryClearCraftStationAssignment(int craftStationId, out int? previousResidentId) => _craftStationService.ClearCraftStationAssignment(craftStationId, out previousResidentId);
    public void ClearSlotAssignmentForResident(int residentId) => _slotService.ClearAssignmentForResident(residentId);
    public void ClearSeatAssignmentForResident(int residentId) => _seatService.ClearAssignmentForResident(residentId);
    public void ClearBedAssignmentForResident(int residentId) => _bedService.ClearAssignmentForResident(residentId);
    public void ClearCraftStationAssignmentForResident(int residentId) => _craftStationService.ClearAssignmentForResident(residentId);
    public bool TryAssignInnkeeperSlot(int residentId, out ZoneSlotData? slotData) => _slotService.TryAssignInnkeeperSlot(residentId, out slotData);
    public bool ForceAssignInnkeeperSlot(int slotId, int residentId, out int? previousResidentId, out ZoneSlotData? slotData) => _slotService.ForceAssignInnkeeperSlot(slotId, residentId, out previousResidentId, out slotData);
    public bool TryAssignBed(int residentId, out RegisteredBedData? bedData) => _bedService.TryAssignBed(residentId, out bedData);
    public bool ForceAssignBed(int bedId, int residentId, out int? previousResidentId, out RegisteredBedData? bedData) => _bedService.ForceAssignBed(bedId, residentId, out previousResidentId, out bedData);
    public bool ForceAssignSeat(int seatId, int residentId, out int? previousResidentId, out RegisteredSeatData? seatData) => _seatService.ForceAssignSeat(seatId, residentId, out previousResidentId, out seatData);
    public bool ForceAssignCraftStation(int craftStationId, int residentId, out int? previousResidentId, out RegisteredCraftStationData? craftStationData) => _craftStationService.ForceAssignCraftStation(craftStationId, residentId, out previousResidentId, out craftStationData);

    public void ClearAllState()
    {
        _waypointService.ClearAllWaypoints();
        _seatService.ClearAllSeats();
        _bedService.ClearAllBeds();
        _craftStationService.ClearAllCraftStations();
        _slotService.ClearAllSlots();
        _zoneService.ClearAllZones();
        _buildingService.ClearAllBuildings();
    }

    public bool TryDeleteZoneAtCrosshair(out SettlementsDeletionReport report)
    {
        report = new SettlementsDeletionReport();
        if (!_zoneService.TryGetPlacementPoint(out var point) || !_zoneService.TryFindZoneAtPoint(point, out var zone))
        {
            return false;
        }

        var deletedSlotIds = _slotService.DeleteSlotsInZone(zone.Id);
        var deletedSeatIds = _seatService.DeleteSeatsInZone(zone.Id);
        var deletedBedIds = _bedService.DeleteBedsInZone(zone.Id);
        var deletedCraftStationIds = _craftStationService.DeleteCraftStationsInZone(zone.Id);

        if (!_zoneService.DeleteZone(zone.Id, out var deletedZone))
        {
            return false;
        }

        if (deletedZone != null)
        {
            _buildingService.DeleteBuildingIfUnused(
                deletedZone.BuildingId,
                _zoneService.Zones,
                _slotService.Slots,
                _seatService.Seats,
                _bedService.Beds,
                _craftStationService.CraftStations);
        }

        report = new SettlementsDeletionReport(
            deletedZoneId: zone.Id,
            deletedSlotIds: deletedSlotIds,
            deletedSeatIds: deletedSeatIds,
            deletedBedIds: deletedBedIds,
            deletedCraftStationIds: deletedCraftStationIds);
        return true;
    }

    public bool TryDeleteSlotAtCrosshair(out SettlementsDeletionReport report)
    {
        report = new SettlementsDeletionReport();
        if (!_slotService.TryGetPlacementPoint(out var point) || !_slotService.TryFindSlotAtPoint(point, out var slot))
        {
            return false;
        }

        if (!_slotService.DeleteSlot(slot.Id))
        {
            return false;
        }

        _buildingService.DeleteBuildingIfUnused(
            slot.BuildingId,
            _zoneService.Zones,
            _slotService.Slots,
            _seatService.Seats,
            _bedService.Beds,
            _craftStationService.CraftStations);

        report = new SettlementsDeletionReport(deletedSlotIds: new[] { slot.Id });
        return true;
    }

    public bool TryDeleteSeatAtCrosshair(out SettlementsDeletionReport report)
    {
        report = new SettlementsDeletionReport();
        if (!_seatService.TryGetSeatAtCrosshair(out var seat))
        {
            return false;
        }

        if (!_seatService.DeleteSeat(seat.Id))
        {
            return false;
        }

        _buildingService.DeleteBuildingIfUnused(
            seat.BuildingId,
            _zoneService.Zones,
            _slotService.Slots,
            _seatService.Seats,
            _bedService.Beds,
            _craftStationService.CraftStations);

        report = new SettlementsDeletionReport(deletedSeatIds: new[] { seat.Id });
        return true;
    }

    public bool TryDeleteBedAtCrosshair(out SettlementsDeletionReport report)
    {
        report = new SettlementsDeletionReport();
        if (!_bedService.TryGetBedAtCrosshair(out var bed))
        {
            return false;
        }

        if (!_bedService.DeleteBed(bed.Id))
        {
            return false;
        }

        _buildingService.DeleteBuildingIfUnused(
            bed.BuildingId,
            _zoneService.Zones,
            _slotService.Slots,
            _seatService.Seats,
            _bedService.Beds,
            _craftStationService.CraftStations);

        report = new SettlementsDeletionReport(deletedBedIds: new[] { bed.Id });
        return true;
    }

    public bool TryDeleteCraftStationAtCrosshair(out SettlementsDeletionReport report)
    {
        report = new SettlementsDeletionReport();
        if (!_craftStationService.TryGetCraftStationAtCrosshair(out var craftStation))
        {
            return false;
        }

        if (!_craftStationService.DeleteCraftStation(craftStation.Id))
        {
            return false;
        }

        _buildingService.DeleteBuildingIfUnused(
            craftStation.BuildingId,
            _zoneService.Zones,
            _slotService.Slots,
            _seatService.Seats,
            _bedService.Beds,
            _craftStationService.CraftStations);

        report = new SettlementsDeletionReport(deletedCraftStationIds: new[] { craftStation.Id });
        return true;
    }

    public bool TryDeleteWaypointAtCrosshair() => _waypointService.DeleteWaypointAtCrosshair();

    public bool TryRestoreResidentAssignment(OccupationTargetKind targetKind, int targetId, int residentId)
    {
        return targetKind switch
        {
            OccupationTargetKind.Slot => _slotService.TryRestoreAssignment(targetId, residentId),
            OccupationTargetKind.Seat => _seatService.TryRestoreAssignment(targetId, residentId),
            OccupationTargetKind.Bed => _bedService.TryRestoreAssignment(targetId, residentId),
            OccupationTargetKind.CraftStation => _craftStationService.TryRestoreAssignment(targetId, residentId),
            _ => false
        };
    }
}
