using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryBuildingService
{
    private readonly ManualLogSource _log;
    private readonly List<BuildingData> _buildings = new();

    private int _nextBuildingId = 1;

    public IReadOnlyList<BuildingData> Buildings => _buildings;

    public RegistryBuildingService(ManualLogSource log)
    {
        _log = log;
    }

    public BuildingData CreateImplicitBuildingForZone(ZoneType zoneType, Vector3 anchorPosition)
    {
        var building = new BuildingData(
            _nextBuildingId++,
            $"{zoneType} Building #{_nextBuildingId - 1}",
            anchorPosition);

        _buildings.Add(building);
        _log.LogInfo($"Created implicit building #{building.Id} for zone type '{zoneType}' at {building.AnchorPosition}.");
        return building;
    }

    public bool DeleteBuildingIfUnused(int buildingId, IReadOnlyList<FunctionalZoneData> zones, IReadOnlyList<ZoneSlotData> slots, IReadOnlyList<RegisteredSeatData> seats)
    {
        if (zones.Any(zone => zone.BuildingId == buildingId) ||
            slots.Any(slot => slot.BuildingId == buildingId) ||
            seats.Any(seat => seat.BuildingId == buildingId))
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
}
