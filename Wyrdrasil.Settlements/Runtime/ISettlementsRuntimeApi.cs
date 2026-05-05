using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Runtime;

public interface ISettlementsRuntimeApi
{
    IReadOnlyList<FunctionalZoneData> Zones { get; }
    IReadOnlyList<NavigationWaypointData> Waypoints { get; }
    int? PendingLinkStartWaypointId { get; }
    IReadOnlyList<ZoneSlotData> Slots { get; }
    IReadOnlyList<RegisteredSeatData> Seats { get; }
    IReadOnlyList<RegisteredBedData> Beds { get; }
    IReadOnlyList<RegisteredCraftStationData> CraftStations { get; }
    bool TryGetRuntimeState(int zoneId, out FunctionalZoneRuntimeState runtimeState);
    FunctionalZoneRuntimeState Evaluate(FunctionalZoneData zone);
    bool IsSeatEligibleForPublicSocialUse(RegisteredSeatData seat);
    bool TryGetSeatById(int seatId, out RegisteredSeatData seatData);
    bool TryGetOccupiedSeatForResident(int residentId, out RegisteredSeatData seatData);
    bool TryGetCraftStationById(int craftStationId, out RegisteredCraftStationData craftStationData);
    bool TryResolveCraftStationAnchor(int craftStationId, out Vector3 anchorWorldPosition, out Vector3 anchorWorldForward);
    bool TryGetCraftStationInteractionProfile(int craftStationId, out CraftStationInteractionProfile profile);
    bool TryClearSlotAssignment(int slotId, out int? previousResidentId);
    bool TryClearSeatAssignment(int seatId, out int? previousResidentId);
    bool TryClearBedAssignment(int bedId, out int? previousResidentId);
    bool TryClearCraftStationAssignment(int craftStationId, out int? previousResidentId);
    void ClearSlotAssignmentForResident(int residentId);
    void ClearSeatAssignmentForResident(int residentId);
    void ClearBedAssignmentForResident(int residentId);
    void ClearCraftStationAssignmentForResident(int residentId);
    bool TryAssignInnkeeperSlot(int residentId, out ZoneSlotData? slotData);
    bool ForceAssignInnkeeperSlot(int slotId, int residentId, out int? previousResidentId, out ZoneSlotData? slotData);
    bool TryAssignBed(int residentId, out RegisteredBedData? bedData);
    bool ForceAssignBed(int bedId, int residentId, out int? previousResidentId, out RegisteredBedData? bedData);
    bool ForceAssignSeat(int seatId, int residentId, out int? previousResidentId, out RegisteredSeatData? seatData);
    bool ForceAssignCraftStation(int craftStationId, int residentId, out int? previousResidentId, out RegisteredCraftStationData? craftStationData);
    void ClearAllState();
    bool TryDeleteZoneAtCrosshair(out SettlementsDeletionReport report);
    bool TryDeleteSlotAtCrosshair(out SettlementsDeletionReport report);
    bool TryDeleteSeatAtCrosshair(out SettlementsDeletionReport report);
    bool TryDeleteBedAtCrosshair(out SettlementsDeletionReport report);
    bool TryDeleteCraftStationAtCrosshair(out SettlementsDeletionReport report);
    bool TryDeleteWaypointAtCrosshair();
    bool TryRestoreResidentAssignment(OccupationTargetKind targetKind, int targetId, int residentId);
}
