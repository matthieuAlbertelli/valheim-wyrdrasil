using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using BepInEx;
using BepInEx.Logging;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryPersistenceService
{
    private const string SaveDirectoryName = "Wyrdrasil.Registry";

    private readonly ManualLogSource _log;
    private readonly RegistryBuildingService _buildingService;
    private readonly RegistryZoneService _zoneService;
    private readonly RegistryWaypointService _waypointService;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryBedService _bedService;
    private readonly RegistryResidentService _residentService;

    private bool _hasLoadedCurrentWorld;
    private string _loadedWorldKey = string.Empty;
    private readonly List<RegisteredSeatSaveData> _pendingSeatResolutions = new();
    private readonly List<RegisteredBedSaveData> _pendingBedResolutions = new();

    public RegistryPersistenceService(
        ManualLogSource log,
        RegistryBuildingService buildingService,
        RegistryZoneService zoneService,
        RegistryWaypointService waypointService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryBedService bedService,
        RegistryResidentService residentService)
    {
        _log = log;
        _buildingService = buildingService;
        _zoneService = zoneService;
        _waypointService = waypointService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _residentService = residentService;
    }

    public void Update()
    {
        var currentWorldKey = GetCurrentWorldKeyOrEmpty();
        if (string.IsNullOrWhiteSpace(currentWorldKey))
        {
            return;
        }

        if (_loadedWorldKey != currentWorldKey)
        {
            _loadedWorldKey = currentWorldKey;
            _hasLoadedCurrentWorld = false;
            _pendingSeatResolutions.Clear();
            _pendingBedResolutions.Clear();
            _log.LogInfo($"Registry persistence detected world '{currentWorldKey}'. Preparing load.");
        }

        if (!_hasLoadedCurrentWorld)
        {
            TryLoadWorldStateForCurrentWorld();
            return;
        }

        RetryPendingAnchorResolutions();
    }

    public void SaveWorldState()
    {
        var currentWorldKey = GetCurrentWorldKeyOrEmpty();
        if (string.IsNullOrWhiteSpace(currentWorldKey))
        {
            _log.LogWarning("Skipped registry save: world is not ready yet.");
            return;
        }

        try
        {
            var saveData = BuildSaveData();
            var path = GetSaveFilePath(currentWorldKey);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializer = new XmlSerializer(typeof(RegistrySaveData));
            using (var stream = File.Create(path))
            {
                serializer.Serialize(stream, saveData);
            }

            _log.LogInfo($"Saved registry world state to '{path}'. buildings={saveData.Buildings.Count}, zones={saveData.Zones.Count}, waypoints={saveData.Waypoints.Count}, waypointLinks={saveData.WaypointLinks.Count}, slots={saveData.Slots.Count}, seats={saveData.Seats.Count}, beds={saveData.Beds.Count}, residents={saveData.Residents.Count}.");
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Failed to save registry world state: {exception.GetType().Name}: {exception.Message}");
        }
    }

    public void DeleteCurrentWorldSave()
    {
        var currentWorldKey = GetCurrentWorldKeyOrEmpty();
        if (string.IsNullOrWhiteSpace(currentWorldKey))
        {
            return;
        }

        var path = GetSaveFilePath(currentWorldKey);
        if (File.Exists(path))
        {
            File.Delete(path);
            _log.LogInfo($"Deleted registry world save '{path}'.");
        }

        _pendingSeatResolutions.Clear();
        _pendingBedResolutions.Clear();
    }

    private void TryLoadWorldStateForCurrentWorld()
    {
        var currentWorldKey = GetCurrentWorldKeyOrEmpty();
        if (string.IsNullOrWhiteSpace(currentWorldKey))
        {
            return;
        }

        var path = GetSaveFilePath(currentWorldKey);
        if (!File.Exists(path))
        {
            _hasLoadedCurrentWorld = true;
            _log.LogInfo($"No registry save file found for world '{currentWorldKey}'. Starting with an empty state.");
            return;
        }

        try
        {
            RegistrySaveData? saveData = null;
            var serializer = new XmlSerializer(typeof(RegistrySaveData));
            using (var stream = File.OpenRead(path))
            {
                var deserialized = serializer.Deserialize(stream);
                if (deserialized is RegistrySaveData typedSaveData)
                {
                    saveData = typedSaveData;
                }
            }

            if (saveData == null)
            {
                _hasLoadedCurrentWorld = true;
                _log.LogWarning($"Registry save file '{path}' could not be deserialized. Starting empty.");
                return;
            }

            LoadFromSaveData(saveData);
            _hasLoadedCurrentWorld = true;
            _log.LogInfo($"Loaded registry world state from '{path}'. buildings={saveData.Buildings.Count}, zones={saveData.Zones.Count}, waypoints={_waypointService.Waypoints.Count}, waypointLinks={_waypointService.GetPersistedLinks().Count}, slots={saveData.Slots.Count}, seats={_seatService.Seats.Count}, beds={_bedService.Beds.Count}, residents={saveData.Residents.Count}.");
        }
        catch (Exception exception)
        {
            _hasLoadedCurrentWorld = true;
            _log.LogWarning($"Failed to load registry world state: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private void LoadFromSaveData(RegistrySaveData saveData)
    {
        _pendingSeatResolutions.Clear();
        _pendingBedResolutions.Clear();
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
        _residentService.LoadResidents(saveData.Residents.Select(ToRegisteredNpcData), saveData.NextResidentId);
        RebuildAssignmentsFromResidents();
        _residentService.RestoreResidentsAfterLoad();
    }

    private void RetryPendingAnchorResolutions()
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

        if (!resolvedAny)
        {
            return;
        }

        RebuildAssignmentsFromResidents();
        _residentService.RestoreResidentsAfterLoad();
        _log.LogInfo($"Resolved deferred anchors. Remaining unresolved seats={_pendingSeatResolutions.Count}, beds={_pendingBedResolutions.Count}.");
    }

    private void RebuildAssignmentsFromResidents()
    {
        foreach (var resident in _residentService.RegisteredNpcs)
        {
            if (resident.AssignedSlotId.HasValue)
            {
                if (!_slotService.TryRestoreAssignment(resident.AssignedSlotId.Value, resident.Id))
                {
                    _log.LogWarning($"Resident #{resident.Id} references missing slot #{resident.AssignedSlotId.Value}. Clearing slot assignment.");
                    resident.ClearAssignedSlot();
                    if (resident.Role == NpcRole.Innkeeper)
                    {
                        resident.SetRole(NpcRole.Villager);
                    }
                }
            }

            if (resident.AssignedSeatId.HasValue && !_seatService.TryRestoreAssignment(resident.AssignedSeatId.Value, resident.Id))
            {
                if (_pendingSeatResolutions.Any(seat => seat.Id == resident.AssignedSeatId.Value))
                {
                    continue;
                }

                _log.LogWarning($"Resident #{resident.Id} references missing seat #{resident.AssignedSeatId.Value}. Clearing seat assignment.");
                resident.ClearAssignedSeat();
            }

            if (resident.AssignedBedId.HasValue && !_bedService.TryRestoreAssignment(resident.AssignedBedId.Value, resident.Id))
            {
                if (_pendingBedResolutions.Any(bed => bed.Id == resident.AssignedBedId.Value))
                {
                    continue;
                }

                _log.LogWarning($"Resident #{resident.Id} references missing bed #{resident.AssignedBedId.Value}. Clearing bed assignment.");
                resident.ClearAssignedBed();
            }
        }
    }

    private RegistrySaveData BuildSaveData()
    {
        _residentService.PrepareResidentPresenceSnapshotsForSave();

        var saveData = new RegistrySaveData
        {
            Version = 2,
            NextBuildingId = _buildingService.NextBuildingId,
            NextZoneId = _zoneService.NextZoneId,
            NextWaypointId = _waypointService.NextWaypointId,
            NextSlotId = _slotService.NextSlotId,
            NextSeatId = Math.Max(_seatService.NextSeatId, _pendingSeatResolutions.Count > 0 ? _pendingSeatResolutions.Max(seat => seat.Id) + 1 : _seatService.NextSeatId),
            NextBedId = Math.Max(_bedService.NextBedId, _pendingBedResolutions.Count > 0 ? _pendingBedResolutions.Max(bed => bed.Id) + 1 : _bedService.NextBedId),
            NextResidentId = _residentService.NextRegisteredNpcId
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

        var persistedSeatIds = new HashSet<int>(saveData.Seats.Select(seat => seat.Id));
        var persistedBedIds = new HashSet<int>(saveData.Beds.Select(bed => bed.Id));
        foreach (var resident in _residentService.RegisteredNpcs)
        {
            if (resident == null || resident.Identity == null || resident.Identity.Appearance == null || resident.Identity.Equipment == null)
            {
                continue;
            }

            var residentSave = FromRegisteredNpcData(resident);
            if (residentSave.AssignedSeatId.HasValue && !persistedSeatIds.Contains(residentSave.AssignedSeatId.Value))
            {
                residentSave.AssignedSeatId = null;
            }

            if (residentSave.AssignedBedId.HasValue && !persistedBedIds.Contains(residentSave.AssignedBedId.Value))
            {
                residentSave.AssignedBedId = null;
            }

            saveData.Residents.Add(residentSave);
        }

        return saveData;
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
        return new ZoneSlotData(data.Id, data.BuildingId, data.ZoneId, data.SlotType, data.Position.ToVector3());
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

    private static RegisteredNpcSaveData FromRegisteredNpcData(RegisteredNpcData data)
    {
        return new RegisteredNpcSaveData
        {
            Id = data.Id,
            DisplayName = data.DisplayName,
            Role = data.Role,
            AssignedSlotId = data.AssignedSlotId,
            AssignedSeatId = data.AssignedSeatId,
            AssignedBedId = data.AssignedBedId,
            Identity = FromIdentityData(data.Identity),
            PresenceSnapshot = FromPresenceSnapshotData(data.PresenceSnapshot),
            ScheduleEntries = data.ScheduleEntries.Select(FromScheduleEntryData).ToList()
        };
    }

    private static RegisteredNpcData ToRegisteredNpcData(RegisteredNpcSaveData data)
    {
        var resident = new RegisteredNpcData(data.Id, data.DisplayName, ToIdentityData(data.Identity));
        resident.SetRole(data.Role);
        if (data.AssignedSlotId.HasValue)
        {
            resident.AssignSlot(data.AssignedSlotId.Value);
        }

        if (data.AssignedSeatId.HasValue)
        {
            resident.AssignSeat(data.AssignedSeatId.Value);
        }

        if (data.AssignedBedId.HasValue)
        {
            resident.AssignBed(data.AssignedBedId.Value);
        }

        resident.SetScheduleEntries(data.ScheduleEntries.Select(ToScheduleEntryData));
        ApplyPresenceSnapshotSaveData(resident.PresenceSnapshot, data.PresenceSnapshot);
        return resident;
    }

    private static ResidentScheduleEntrySaveData FromScheduleEntryData(ResidentScheduleEntryData data)
    {
        return new ResidentScheduleEntrySaveData
        {
            ActivityType = data.ActivityType,
            StartMinuteOfDay = data.StartMinuteOfDay,
            EndMinuteOfDay = data.EndMinuteOfDay,
            Priority = data.Priority
        };
    }

    private static ResidentScheduleEntryData ToScheduleEntryData(ResidentScheduleEntrySaveData data)
    {
        return new ResidentScheduleEntryData(data.ActivityType, data.StartMinuteOfDay, data.EndMinuteOfDay, data.Priority);
    }

    private static ResidentPresenceSnapshotSaveData FromPresenceSnapshotData(ResidentPresenceSnapshotData data)
    {
        return new ResidentPresenceSnapshotSaveData
        {
            RestoreMode = data.RestoreMode,
            WorldPosition = Float3SaveData.FromVector3(data.WorldPosition),
            WorldYawDegrees = data.WorldYawDegrees
        };
    }

    private static void ApplyPresenceSnapshotSaveData(ResidentPresenceSnapshotData target, ResidentPresenceSnapshotSaveData saveData)
    {
        switch (saveData.RestoreMode)
        {
            case ResidentRestoreMode.WorldPosition:
                target.SetWorldPosition(saveData.WorldPosition.ToVector3(), saveData.WorldYawDegrees);
                break;
            case ResidentRestoreMode.AssignedSlotAnchor:
                target.SetAssignedSlotAnchor(saveData.WorldPosition.ToVector3(), saveData.WorldYawDegrees);
                break;
            case ResidentRestoreMode.AssignedSeatAnchor:
                target.SetAssignedSeatAnchor(saveData.WorldPosition.ToVector3(), saveData.WorldYawDegrees);
                break;
            case ResidentRestoreMode.AssignedBedAnchor:
                target.SetAssignedBedAnchor(saveData.WorldPosition.ToVector3(), saveData.WorldYawDegrees);
                break;
            default:
                target.Clear();
                break;
        }
    }

    private static VikingIdentitySaveData FromIdentityData(VikingIdentityData data)
    {
        return new VikingIdentitySaveData
        {
            GenerationSeed = data.GenerationSeed,
            Role = data.Role,
            Appearance = new VikingAppearanceSaveData
            {
                ModelIndex = data.Appearance.ModelIndex,
                HairItem = data.Appearance.HairItem,
                BeardItem = data.Appearance.BeardItem ?? string.Empty,
                HasBeardItem = data.Appearance.BeardItem != null,
                SkinColor = ColorSaveData.FromColor(data.Appearance.SkinColor),
                HairColor = ColorSaveData.FromColor(data.Appearance.HairColor)
            },
            Equipment = new VikingEquipmentSaveData
            {
                HelmetItem = data.Equipment.HelmetItem ?? string.Empty,
                HasHelmetItem = data.Equipment.HelmetItem != null,
                ChestItem = data.Equipment.ChestItem ?? string.Empty,
                HasChestItem = data.Equipment.ChestItem != null,
                LegItem = data.Equipment.LegItem ?? string.Empty,
                HasLegItem = data.Equipment.LegItem != null,
                ShoulderItem = data.Equipment.ShoulderItem ?? string.Empty,
                HasShoulderItem = data.Equipment.ShoulderItem != null,
                RightHandItem = data.Equipment.RightHandItem ?? string.Empty,
                HasRightHandItem = data.Equipment.RightHandItem != null,
                LeftHandItem = data.Equipment.LeftHandItem ?? string.Empty,
                HasLeftHandItem = data.Equipment.LeftHandItem != null
            }
        };
    }

    private static VikingIdentityData ToIdentityData(VikingIdentitySaveData data)
    {
        var appearance = new VikingAppearanceData(
            data.Appearance.ModelIndex,
            data.Appearance.HairItem,
            data.Appearance.HasBeardItem ? data.Appearance.BeardItem : null,
            data.Appearance.SkinColor.ToColor(),
            data.Appearance.HairColor.ToColor());

        var equipment = new VikingEquipmentData(
            data.Equipment.HasHelmetItem ? data.Equipment.HelmetItem : null,
            data.Equipment.HasChestItem ? data.Equipment.ChestItem : null,
            data.Equipment.HasLegItem ? data.Equipment.LegItem : null,
            data.Equipment.HasShoulderItem ? data.Equipment.ShoulderItem : null,
            data.Equipment.HasRightHandItem ? data.Equipment.RightHandItem : null,
            data.Equipment.HasLeftHandItem ? data.Equipment.LeftHandItem : null);

        return new VikingIdentityData(data.GenerationSeed, data.Role, appearance, equipment);
    }

    private static string GetCurrentWorldKeyOrEmpty()
    {
        if (Player.m_localPlayer == null || ZNet.instance == null)
        {
            return string.Empty;
        }

        try
        {
            return SanitizeWorldKey(ZNet.instance.GetWorldName());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetSaveFilePath(string worldKey)
    {
        var configPath = Paths.ConfigPath;
        return Path.Combine(configPath, SaveDirectoryName, worldKey + ".xml");
    }

    private static string SanitizeWorldKey(string worldName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(worldName.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "world" : sanitized;
    }
}
