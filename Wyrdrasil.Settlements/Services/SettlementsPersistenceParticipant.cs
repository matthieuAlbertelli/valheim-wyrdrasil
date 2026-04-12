using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;


public sealed class SettlementsPersistenceParticipant : IWorldPersistenceParticipant
{
    private readonly ManualLogSource _log;
    private readonly BuildingService _buildingService;
    private readonly FunctionalZoneService _zoneService;
    private readonly NavigationWaypointService _waypointService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly List<RegisteredSeatSaveData> _pendingSeatResolutions = new();
    private readonly List<RegisteredBedSaveData> _pendingBedResolutions = new();
    private readonly List<RegisteredCraftStationSaveData> _pendingCraftStationResolutions = new();

    public SettlementsPersistenceParticipant(
        ManualLogSource log,
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService)
    {
        _log = log;
        _buildingService = buildingService;
        _zoneService = zoneService;
        _waypointService = waypointService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
    }

    public string ModuleId => "settlements";
    public int SchemaVersion => 2;

    public void ResetForWorldChange()
    {
        _pendingSeatResolutions.Clear();
        _pendingBedResolutions.Clear();
        _pendingCraftStationResolutions.Clear();
    }

    public string CapturePayload()
    {
        var saveData = new SettlementsModuleSaveData
        {
            NextBuildingId = _buildingService.NextBuildingId,
            NextZoneId = _zoneService.NextZoneId,
            NextWaypointId = _waypointService.NextWaypointId,
            NextSlotId = _slotService.NextSlotId,
            NextSeatId = Math.Max(_seatService.NextSeatId, _pendingSeatResolutions.Count > 0 ? _pendingSeatResolutions.Max(seat => seat.Id) + 1 : _seatService.NextSeatId),
            NextBedId = Math.Max(_bedService.NextBedId, _pendingBedResolutions.Count > 0 ? _pendingBedResolutions.Max(bed => bed.Id) + 1 : _bedService.NextBedId),
            NextCraftStationId = Math.Max(_craftStationService.NextCraftStationId, _pendingCraftStationResolutions.Count > 0 ? _pendingCraftStationResolutions.Max(station => station.Id) + 1 : _craftStationService.NextCraftStationId)
        };

        foreach (var building in _buildingService.Buildings)
        {
            if (building != null)
            {
                saveData.Buildings.Add(FromBuildingData(building));
            }
        }

        foreach (var zone in _zoneService.Zones)
        {
            if (zone != null)
            {
                saveData.Zones.Add(FromFunctionalZoneData(zone));
            }
        }

        foreach (var waypoint in _waypointService.Waypoints)
        {
            if (waypoint != null)
            {
                saveData.Waypoints.Add(FromNavigationWaypointData(waypoint));
            }
        }

        saveData.WaypointLinks.AddRange(_waypointService.GetPersistedLinks());

        foreach (var slot in _slotService.Slots)
        {
            if (slot != null)
            {
                saveData.Slots.Add(FromZoneSlotData(slot));
            }
        }

        var serializedSeatIds = new HashSet<int>();
        foreach (var seat in _seatService.Seats)
        {
            if (seat == null)
            {
                continue;
            }

            var seatSave = FromRegisteredSeatData(seat);
            saveData.Seats.Add(seatSave);
            serializedSeatIds.Add(seat.Id);
        }

        foreach (var pendingSeat in _pendingSeatResolutions)
        {
            if (serializedSeatIds.Add(pendingSeat.Id))
            {
                saveData.Seats.Add(pendingSeat);
            }
        }

        var serializedBedIds = new HashSet<int>();
        foreach (var bed in _bedService.Beds)
        {
            if (bed == null)
            {
                continue;
            }

            var bedSave = FromRegisteredBedData(bed);
            saveData.Beds.Add(bedSave);
            serializedBedIds.Add(bed.Id);
        }

        foreach (var pendingBed in _pendingBedResolutions)
        {
            if (serializedBedIds.Add(pendingBed.Id))
            {
                saveData.Beds.Add(pendingBed);
            }
        }

        var serializedCraftStationIds = new HashSet<int>();
        foreach (var craftStation in _craftStationService.CraftStations)
        {
            if (craftStation == null)
            {
                continue;
            }

            var craftStationSave = FromRegisteredCraftStationData(craftStation);
            saveData.CraftStations.Add(craftStationSave);
            serializedCraftStationIds.Add(craftStation.Id);
        }

        foreach (var pendingCraftStation in _pendingCraftStationResolutions)
        {
            if (serializedCraftStationIds.Add(pendingCraftStation.Id))
            {
                saveData.CraftStations.Add(pendingCraftStation);
            }
        }

        return WorldPersistenceCoordinator.SerializePayload(saveData);
    }

    public void RestorePayload(string payloadXml)
    {
        ResetForWorldChange();

        var saveData = WorldPersistenceCoordinator.DeserializePayload<SettlementsModuleSaveData>(payloadXml);
        if (saveData == null)
        {
            _buildingService.LoadBuildings(Array.Empty<BuildingData>(), 1);
            _zoneService.LoadZones(Array.Empty<FunctionalZoneData>(), 1);
            _waypointService.LoadWaypoints(Array.Empty<NavigationWaypointData>(), Array.Empty<NavigationWaypointLinkSaveData>(), 1);
            _slotService.LoadSlots(Array.Empty<ZoneSlotData>(), 1);
            _seatService.LoadSeats(Array.Empty<RegisteredSeatData>(), 1);
            _bedService.LoadBeds(Array.Empty<RegisteredBedData>(), 1);
            _craftStationService.LoadCraftStations(Array.Empty<RegisteredCraftStationData>(), 1);
            return;
        }

        _buildingService.LoadBuildings(saveData.Buildings.Select(ToBuildingData), saveData.NextBuildingId);
        _zoneService.LoadZones(saveData.Zones.Select(ToFunctionalZoneData), saveData.NextZoneId);
        _waypointService.LoadWaypoints(saveData.Waypoints.Select(ToNavigationWaypointData), saveData.WaypointLinks, saveData.NextWaypointId);
        _slotService.LoadSlots(saveData.Slots.Select(ToZoneSlotData), saveData.NextSlotId);

        var resolvedSeats = new List<RegisteredSeatData>();
        foreach (var seatSave in saveData.Seats)
        {
            if (_seatService.TryResolveSeatFromSave(seatSave, out var seatData))
            {
                resolvedSeats.Add(seatData);
            }
            else
            {
                _pendingSeatResolutions.Add(seatSave);
            }
        }

        _seatService.LoadSeats(resolvedSeats, saveData.NextSeatId);

        var resolvedBeds = new List<RegisteredBedData>();
        foreach (var bedSave in saveData.Beds)
        {
            if (_bedService.TryResolveBedFromSave(bedSave, out var bedData))
            {
                resolvedBeds.Add(bedData);
            }
            else
            {
                _pendingBedResolutions.Add(bedSave);
            }
        }

        _bedService.LoadBeds(resolvedBeds, saveData.NextBedId);

        var resolvedCraftStations = new List<RegisteredCraftStationData>();
        foreach (var craftStationSave in saveData.CraftStations)
        {
            if (_craftStationService.TryResolveCraftStationFromSave(craftStationSave, out var craftStationData))
            {
                resolvedCraftStations.Add(craftStationData);
            }
            else
            {
                _pendingCraftStationResolutions.Add(craftStationSave);
            }
        }

        _craftStationService.LoadCraftStations(resolvedCraftStations, saveData.NextCraftStationId);
    }

    public bool RetryDeferredResolutions()
    {
        var resolvedAny = false;

        if (_pendingSeatResolutions.Count > 0)
        {
            var resolvedSeatData = new List<RegisteredSeatData>();
            var stillPendingSeats = new List<RegisteredSeatSaveData>();
            foreach (var pendingSeat in _pendingSeatResolutions)
            {
                if (_seatService.TryResolveSeatFromSave(pendingSeat, out var seatData))
                {
                    resolvedSeatData.Add(seatData);
                }
                else
                {
                    stillPendingSeats.Add(pendingSeat);
                }
            }

            if (resolvedSeatData.Count > 0)
            {
                var mergedSeats = _seatService.Seats.ToList();
                mergedSeats.AddRange(resolvedSeatData.Where(candidate => mergedSeats.All(existing => existing.Id != candidate.Id)));
                _seatService.LoadSeats(mergedSeats, Math.Max(_seatService.NextSeatId, mergedSeats.Count + 1));
                resolvedAny = true;
            }

            _pendingSeatResolutions.Clear();
            _pendingSeatResolutions.AddRange(stillPendingSeats);
        }

        if (_pendingBedResolutions.Count > 0)
        {
            var resolvedBedData = new List<RegisteredBedData>();
            var stillPendingBeds = new List<RegisteredBedSaveData>();
            foreach (var pendingBed in _pendingBedResolutions)
            {
                if (_bedService.TryResolveBedFromSave(pendingBed, out var bedData))
                {
                    resolvedBedData.Add(bedData);
                }
                else
                {
                    stillPendingBeds.Add(pendingBed);
                }
            }

            if (resolvedBedData.Count > 0)
            {
                var mergedBeds = _bedService.Beds.ToList();
                mergedBeds.AddRange(resolvedBedData.Where(candidate => mergedBeds.All(existing => existing.Id != candidate.Id)));
                _bedService.LoadBeds(mergedBeds, Math.Max(_bedService.NextBedId, mergedBeds.Count + 1));
                resolvedAny = true;
            }

            _pendingBedResolutions.Clear();
            _pendingBedResolutions.AddRange(stillPendingBeds);
        }

        if (resolvedAny)
        {
            _log.LogInfo($"Resolved deferred settlement anchors. Remaining unresolved seats={_pendingSeatResolutions.Count}, beds={_pendingBedResolutions.Count}, craftStations={_pendingCraftStationResolutions.Count}.");
        }

        if (_pendingCraftStationResolutions.Count > 0)
        {
            var resolvedCraftStationData = new List<RegisteredCraftStationData>();
            var stillPendingCraftStations = new List<RegisteredCraftStationSaveData>();
            foreach (var pendingCraftStation in _pendingCraftStationResolutions)
            {
                if (_craftStationService.TryResolveCraftStationFromSave(pendingCraftStation, out var craftStationData))
                {
                    resolvedCraftStationData.Add(craftStationData);
                }
                else
                {
                    stillPendingCraftStations.Add(pendingCraftStation);
                }
            }

            if (resolvedCraftStationData.Count > 0)
            {
                var mergedCraftStations = _craftStationService.CraftStations.ToList();
                mergedCraftStations.AddRange(resolvedCraftStationData.Where(candidate => mergedCraftStations.All(existing => existing.Id != candidate.Id)));
                _craftStationService.LoadCraftStations(mergedCraftStations, Math.Max(_craftStationService.NextCraftStationId, mergedCraftStations.Count + 1));
                resolvedAny = true;
            }

            _pendingCraftStationResolutions.Clear();
            _pendingCraftStationResolutions.AddRange(stillPendingCraftStations);
        }

        return resolvedAny;
    }

    private static BuildingSaveData FromBuildingData(BuildingData data)
    {
        return new BuildingSaveData
        {
            Id = data.Id,
            DisplayName = data.DisplayName,
            AnchorPosition = Float3SaveData.FromVector3(data.AnchorPosition)
        };
    }

    private static BuildingData ToBuildingData(BuildingSaveData data)
    {
        return new BuildingData(data.Id, data.DisplayName, data.AnchorPosition.ToVector3());
    }

    private static FunctionalZoneSaveData FromFunctionalZoneData(FunctionalZoneData data)
    {
        return new FunctionalZoneSaveData
        {
            Id = data.Id,
            BuildingId = data.BuildingId,
            ZoneType = data.ZoneType,
            Position = Float3SaveData.FromVector3(data.Position),
            FootprintPoints = data.FootprintPoints.Select(Float2SaveData.FromVector2).ToList(),
            BaseY = data.BaseY,
            TopY = data.TopY,
            LevelIndex = data.LevelIndex
        };
    }

    private static FunctionalZoneData ToFunctionalZoneData(FunctionalZoneSaveData data)
    {
        return new FunctionalZoneData(data.Id, data.BuildingId, data.ZoneType, data.Position.ToVector3(), data.FootprintPoints.Select(point => point.ToVector2()), data.BaseY, data.TopY, data.LevelIndex);
    }

    private static NavigationWaypointSaveData FromNavigationWaypointData(NavigationWaypointData data)
    {
        return new NavigationWaypointSaveData
        {
            Id = data.Id,
            Position = Float3SaveData.FromVector3(data.Position)
        };
    }

    private static NavigationWaypointData ToNavigationWaypointData(NavigationWaypointSaveData data)
    {
        return new NavigationWaypointData(data.Id, data.Position.ToVector3());
    }

    private static ZoneSlotSaveData FromZoneSlotData(ZoneSlotData data)
    {
        return new ZoneSlotSaveData
        {
            Id = data.Id,
            BuildingId = data.BuildingId,
            ZoneId = data.ZoneId,
            SlotType = data.SlotType,
            Position = Float3SaveData.FromVector3(data.Position)
        };
    }

    private static ZoneSlotData ToZoneSlotData(ZoneSlotSaveData data)
    {
        return new ZoneSlotData(
            data.Id,
            data.BuildingId,
            data.ZoneId,
            data.SlotType,
            data.Position.ToVector3(),
            data.FacingDirection.ToVector3());
    }

    private static RegisteredSeatSaveData FromRegisteredSeatData(RegisteredSeatData data)
    {
        return new RegisteredSeatSaveData
        {
            Id = data.Id,
            BuildingId = data.BuildingId,
            ZoneId = data.ZoneId,
            UsageType = data.UsageType,
            DisplayName = data.DisplayName,
            PersistentFurnitureId = data.PersistentFurnitureId,
            SeatPosition = Float3SaveData.FromVector3(data.SeatPosition)
        };
    }

    private static RegisteredBedSaveData FromRegisteredBedData(RegisteredBedData data)
    {
        return new RegisteredBedSaveData
        {
            Id = data.Id,
            BuildingId = data.BuildingId,
            ZoneId = data.ZoneId,
            DisplayName = data.DisplayName,
            PersistentFurnitureId = data.PersistentFurnitureId,
            SleepPosition = Float3SaveData.FromVector3(data.SleepPosition),
            SleepForward = Float3SaveData.FromVector3(data.SleepForward)
        };
    }

    private static RegisteredCraftStationSaveData FromRegisteredCraftStationData(RegisteredCraftStationData data)
    {
        return new RegisteredCraftStationSaveData
        {
            Id = data.Id,
            BuildingId = data.BuildingId,
            ZoneId = data.ZoneId,
            DisplayName = data.DisplayName,
            PersistentFurnitureId = data.PersistentFurnitureId,
            ApproachPosition = Float3SaveData.FromVector3(data.ApproachPosition),
            UsePosition = Float3SaveData.FromVector3(data.UsePosition),
            UseForward = Float3SaveData.FromVector3(data.UseForward),
            AssignedRegisteredNpcId = data.AssignedRegisteredNpcId
        };
    }

}
