using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Authoring;

public interface ISettlementsAuthoringApi
{
    bool IsZoneAuthoringActive { get; }
    bool IsZoneHeightEditingActive { get; }
    PendingZoneAuthoringSnapshot? GetPendingZoneAuthoringSnapshot();
    void CreateTavernZone();
    void CreateBedroomZone();
    void UpdatePendingZoneAuthoringPreview();
    void HandleZoneAuthoringSecondaryInput();
    void AdjustPendingZoneHeight(int direction, bool adjustBase);
    void CancelPendingZoneAuthoring();
    void SetZoneAuthoringVisualsVisible(bool visible);
    bool TryGetPlacementPoint(out Vector3 placementPoint);
    bool TryFindZoneAtPoint(Vector3 point, out FunctionalZoneData zone);
    void UpdateTargetedZoneHighlight();
    void CreateNavigationWaypoint();
    void ConnectNavigationWaypoints();
    void CreateInnkeeperSlot();
    bool TryGetSlotAtCrosshair(out ZoneSlotData slotData);
    bool TryGetSeatAtCrosshair(out RegisteredSeatData seatData);
    bool TryGetBedAtCrosshair(out RegisteredBedData bedData);
    bool TryGetCraftStationAtCrosshair(out RegisteredCraftStationData craftStationData);
    void DesignateSeatAtCrosshair();
    void DesignateBedAtCrosshair();
    void DesignateCraftStationAtCrosshair();
    bool TryGetOrDesignateCraftStationAtCrosshair(out RegisteredCraftStationData craftStationData, out string failureReason);
}
