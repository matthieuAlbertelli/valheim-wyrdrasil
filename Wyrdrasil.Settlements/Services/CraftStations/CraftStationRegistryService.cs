using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services.CraftStations;

public sealed class CraftStationRegistryService
{
    private readonly ManualLogSource _log;
    private readonly List<RegisteredCraftStationData> _craftStations = new();
    private int _nextCraftStationId = 1;

    public CraftStationRegistryService(ManualLogSource log)
    {
        _log = log;
    }

    public IReadOnlyList<RegisteredCraftStationData> CraftStations => _craftStations;
    public int NextCraftStationId => _nextCraftStationId;

    public void LoadCraftStations(IEnumerable<RegisteredCraftStationData> craftStations, int nextCraftStationId)
    {
        _craftStations.Clear();
        _craftStations.AddRange(craftStations);
        _nextCraftStationId = nextCraftStationId;
    }

    public void ClearAllCraftStations()
    {
        _craftStations.Clear();
        _nextCraftStationId = 1;
    }

    public bool TryRestoreAssignment(int craftStationId, int residentId, out RegisteredCraftStationData? craftStationData)
    {
        craftStationData = _craftStations.FirstOrDefault(candidate => candidate.Id == craftStationId);
        if (craftStationData == null)
        {
            return false;
        }

        craftStationData.AssignRegisteredNpc(residentId);
        return true;
    }

    public bool TryGetCraftStationById(int craftStationId, out RegisteredCraftStationData craftStationData)
    {
        var station = _craftStations.FirstOrDefault(candidate => candidate.Id == craftStationId);
        if (station == null)
        {
            craftStationData = null!;
            return false;
        }

        craftStationData = station;
        return true;
    }

    public RegisteredCraftStationData? FindCraftStationByFurniture(GameObject furnitureRoot)
    {
        return _craftStations.FirstOrDefault(candidate => candidate.FurnitureRoot != null && candidate.FurnitureRoot == furnitureRoot);
    }

    public RegisteredCraftStationData AddCraftStation(
        int buildingId,
        int? zoneId,
        string displayName,
        string persistentFurnitureId,
        Vector3 referenceWorldPosition,
        Vector3 anchorLocalPosition,
        Vector3 anchorLocalForward,
        string interactionProfileId,
        GameObject furnitureRoot,
        CraftingStation craftingStation)
    {
        var station = new RegisteredCraftStationData(
            _nextCraftStationId++,
            buildingId,
            zoneId,
            displayName,
            persistentFurnitureId,
            referenceWorldPosition,
            anchorLocalPosition,
            anchorLocalForward,
            interactionProfileId);

        station.UpdateRuntimeBinding(furnitureRoot, craftingStation);
        _craftStations.Add(station);
        return station;
    }

    public void AddResolvedCraftStation(RegisteredCraftStationData craftStationData)
    {
        _craftStations.Add(craftStationData);
    }

    public bool ForceAssignCraftStation(int craftStationId, int registeredNpcId, out int? previousResidentId, out RegisteredCraftStationData? craftStationData)
    {
        foreach (var station in _craftStations)
        {
            if (station.Id != craftStationId)
            {
                continue;
            }

            previousResidentId = station.AssignedRegisteredNpcId;
            station.AssignRegisteredNpc(registeredNpcId);
            craftStationData = station;
            _log.LogInfo($"[CraftStation][Assignment] Assigned resident #{registeredNpcId} to station #{station.Id}. previousResident={previousResidentId?.ToString() ?? "none"}");
            return true;
        }

        previousResidentId = null;
        craftStationData = null;
        return false;
    }

    public bool ClearCraftStationAssignment(int craftStationId, out int? previousResidentId, out RegisteredCraftStationData? craftStationData)
    {
        foreach (var station in _craftStations)
        {
            if (station.Id != craftStationId)
            {
                continue;
            }

            previousResidentId = station.AssignedRegisteredNpcId;
            if (!previousResidentId.HasValue)
            {
                craftStationData = null;
                return false;
            }

            station.ClearAssignedRegisteredNpc();
            craftStationData = station;
            _log.LogInfo($"[CraftStation][Assignment] Cleared assignment on station #{station.Id}. previousResident={previousResidentId.Value}");
            return true;
        }

        previousResidentId = null;
        craftStationData = null;
        return false;
    }

    public IReadOnlyList<RegisteredCraftStationData> ClearAssignmentForResident(int registeredNpcId)
    {
        var clearedStations = new List<RegisteredCraftStationData>();
        foreach (var station in _craftStations.Where(candidate => candidate.AssignedRegisteredNpcId == registeredNpcId))
        {
            station.ClearAssignedRegisteredNpc();
            clearedStations.Add(station);
            _log.LogInfo($"[CraftStation][Assignment] Cleared resident #{registeredNpcId} from station #{station.Id}.");
        }

        return clearedStations;
    }

    public IReadOnlyList<int> DeleteCraftStationsInZone(int zoneId)
    {
        var stationIds = new List<int>();
        foreach (var station in _craftStations.Where(candidate => candidate.ZoneId == zoneId).ToList())
        {
            stationIds.Add(station.Id);
            DeleteCraftStation(station.Id, out _);
        }

        return stationIds;
    }

    public bool DeleteCraftStation(int craftStationId, out RegisteredCraftStationData? removedCraftStation)
    {
        var index = _craftStations.FindIndex(candidate => candidate.Id == craftStationId);
        if (index < 0)
        {
            removedCraftStation = null;
            return false;
        }

        removedCraftStation = _craftStations[index];
        _craftStations.RemoveAt(index);
        _log.LogInfo($"[CraftStation][Authoring] Deleted station #{removedCraftStation.Id} ('{removedCraftStation.DisplayName}').");
        return true;
    }
}
