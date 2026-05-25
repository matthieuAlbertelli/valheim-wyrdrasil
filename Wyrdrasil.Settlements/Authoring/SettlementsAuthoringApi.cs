using UnityEngine;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Authoring;

public sealed class SettlementsAuthoringApi : ISettlementsAuthoringApi
{
    private readonly BuildingService _buildingService;
    private readonly FunctionalZoneService _zoneService;
    private readonly NavigationWaypointService _waypointService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;

    public SettlementsAuthoringApi(
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService)
    {
        _buildingService = buildingService;
        _zoneService = zoneService;
        _waypointService = waypointService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
    }

    public bool IsZoneAuthoringActive => _zoneService.IsZoneAuthoringActive;
    public bool IsZoneHeightEditingActive => _zoneService.IsZoneHeightEditingActive;
    public bool IsBuildingAuthoringActive => _buildingService.IsBuildingAuthoringActive;
    public bool IsBuildingHeightEditingActive => _buildingService.IsBuildingHeightEditingActive;
    public PendingZoneAuthoringSnapshot? GetPendingZoneAuthoringSnapshot() => _zoneService.GetPendingZoneAuthoringSnapshot();
    public PendingZoneAuthoringSnapshot? GetPendingBuildingAuthoringSnapshot() => _buildingService.GetPendingBuildingAuthoringSnapshot();
    public void CreateTavernZone() => _zoneService.CreateTavernZone();
    public void CreateBedroomZone() => _zoneService.CreateBedroomZone();
    public void UpdatePendingZoneAuthoringPreview() => _zoneService.UpdatePendingZoneAuthoringPreview();
    public void HandleZoneAuthoringSecondaryInput() => _zoneService.HandleZoneAuthoringSecondaryInput();
    public void AdjustPendingZoneHeight(int direction, bool adjustBase) => _zoneService.AdjustPendingZoneHeight(direction, adjustBase);
    public void CancelPendingZoneAuthoring() => _zoneService.CancelPendingZoneAuthoring();
    public void SetZoneAuthoringVisualsVisible(bool visible) => _zoneService.SetPlayerAuthoringVisualsVisible(visible);
    public bool AdvanceBuildingAuthoring() => _buildingService.HandleBuildingAuthoringPrimaryInput();
    public void UpdatePendingBuildingAuthoringPreview() => _buildingService.UpdatePendingBuildingAuthoringPreview();
    public void HandleBuildingAuthoringSecondaryInput() => _buildingService.HandleBuildingAuthoringSecondaryInput();
    public void AdjustPendingBuildingHeight(int direction, bool adjustBase) => _buildingService.AdjustPendingBuildingHeight(direction, adjustBase);
    public void CancelPendingBuildingAuthoring() => _buildingService.CancelPendingBuildingAuthoring();
    public void SetBuildingAuthoringVisualsVisible(bool visible) => _buildingService.SetPlayerAuthoringVisualsVisible(visible);
    public bool TryGetPlacementPoint(out Vector3 placementPoint) => _zoneService.TryGetPlacementPoint(out placementPoint);
    public bool TryFindZoneAtPoint(Vector3 point, out FunctionalZoneData zone) => _zoneService.TryFindZoneAtPoint(point, out zone);
    public bool TryFindBuildingAtPoint(Vector3 point, out BuildingData building) => _buildingService.TryFindBuildingAtPoint(point, out building);
    public bool TryGetBuildingAtCrosshair(out BuildingData building) => _buildingService.TryGetBuildingAtCrosshair(out building);
    public void UpdateTargetedZoneHighlight() => _zoneService.UpdateTargetedZoneHighlight();
    public void UpdateTargetedBuildingHighlight() => _buildingService.UpdateTargetedBuildingHighlight();
    public void CreateNavigationWaypoint() => _waypointService.CreateNavigationWaypoint();
    public void ConnectNavigationWaypoints() => _waypointService.ConnectNavigationWaypoints();
    public void CreateInnkeeperSlot() => _slotService.CreateInnkeeperSlot();
    public bool TryGetSlotAtCrosshair(out ZoneSlotData slotData) => _slotService.TryGetSlotAtCrosshair(out slotData);
    public bool TryGetSeatAtCrosshair(out RegisteredSeatData seatData) => _seatService.TryGetSeatAtCrosshair(out seatData);
    public bool TryGetBedAtCrosshair(out RegisteredBedData bedData) => _bedService.TryGetBedAtCrosshair(out bedData);
    public bool TryGetCraftStationAtCrosshair(out RegisteredCraftStationData craftStationData) => _craftStationService.TryGetCraftStationAtCrosshair(out craftStationData);
    public void DesignateSeatAtCrosshair() => _seatService.DesignateSeatAtCrosshair();
    public void DesignateBedAtCrosshair() => _bedService.DesignateBedAtCrosshair();
    public void DesignateCraftStationAtCrosshair() => _craftStationService.DesignateCraftStationAtCrosshair();
    public bool TryGetOrDesignateCraftStationAtCrosshair(out RegisteredCraftStationData craftStationData, out string failureReason) =>
        _craftStationService.TryGetOrDesignateCraftStationAtCrosshair(out craftStationData, out failureReason);
}
