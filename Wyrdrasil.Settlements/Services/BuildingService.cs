using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class BuildingService
{
    private readonly ManualLogSource _log;
    private readonly List<BuildingData> _buildings = new();

    private int _nextBuildingId = 1;

    public IReadOnlyList<BuildingData> Buildings => _buildings;
    public int NextBuildingId => _nextBuildingId;

    public BuildingService(ManualLogSource log)
    {
        _log = log;
    }

    public BuildingData CreateImplicitBuildingForZone(ZoneType zoneType, Vector3 anchorPosition)
    {
        return CreateImplicitBuilding($"{zoneType} Building", anchorPosition, $"zone type '{zoneType}'");
    }

    public BuildingData CreateImplicitBuildingForDesignation(string designationLabel, Vector3 anchorPosition)
    {
        return CreateImplicitBuilding($"{designationLabel} Building", anchorPosition, $"standalone designation '{designationLabel}'");
    }

    public void LoadBuildings(IEnumerable<BuildingData> buildings, int nextBuildingId)
    {
        _buildings.Clear();
        _buildings.AddRange(buildings);
        _nextBuildingId = nextBuildingId;
    }

    public void ClearAllBuildings()
    {
        _buildings.Clear();
        _nextBuildingId = 1;
    }

    public bool DeleteBuildingIfUnused(
        int buildingId,
        IReadOnlyList<FunctionalZoneData> zones,
        IReadOnlyList<ZoneSlotData> slots,
        IReadOnlyList<RegisteredSeatData> seats,
        IReadOnlyList<RegisteredBedData> beds,
        IReadOnlyList<RegisteredCraftStationData> craftStations)
    {
        if (zones.Any(zone => zone.BuildingId == buildingId) ||
            slots.Any(slot => slot.BuildingId == buildingId) ||
            seats.Any(seat => seat.BuildingId == buildingId) ||
            beds.Any(bed => bed.BuildingId == buildingId) ||
            craftStations.Any(station => station.BuildingId == buildingId))
        {
            return false;
        }

        var removed = _buildings.RemoveAll(building => building.Id == buildingId) > 0;
        if (removed)
        {
            _log.LogInfo($"Deleted orphaned building #{buildingId}.");
        }

        return removed;
    }

    private BuildingData CreateImplicitBuilding(string label, Vector3 anchorPosition, string logContext)
    {
        var building = new BuildingData(
            _nextBuildingId++,
            $"{label} #{_nextBuildingId - 1}",
            anchorPosition);

        _buildings.Add(building);
        _log.LogInfo($"Created implicit building #{building.Id} for {logContext} at {building.AnchorPosition}.");
        return building;
    }
}
