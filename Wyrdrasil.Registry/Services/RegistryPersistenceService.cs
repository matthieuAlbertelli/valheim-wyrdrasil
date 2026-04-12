using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using BepInEx;
using BepInEx.Logging;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryPersistenceService
{
    private const string SaveDirectoryName = "Wyrdrasil.Registry";

    private readonly ManualLogSource _log;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly RegistryResidentService _residentService;
    private readonly ResidentRoutineService _residentRoutineService;
    private readonly WorldPersistenceCoordinator _coordinator;
    private readonly IReadOnlyList<IWorldPersistenceParticipant> _participants;

    private bool _hasLoadedCurrentWorld;
    private string _loadedWorldKey = string.Empty;

    public RegistryPersistenceService(
        ManualLogSource log,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        RegistryResidentService residentService,
        ResidentRoutineService residentRoutineService,
        WorldPersistenceCoordinator coordinator,
        IReadOnlyList<IWorldPersistenceParticipant> participants)
    {
        _log = log;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _residentService = residentService;
        _residentRoutineService = residentRoutineService;
        _coordinator = coordinator;
        _participants = participants;
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
            foreach (var participant in _participants)
            {
                participant.ResetForWorldChange();
            }

            _log.LogInfo($"Registry persistence detected world '{currentWorldKey}'. Preparing modular load.");
        }

        if (!_hasLoadedCurrentWorld)
        {
            TryLoadWorldStateForCurrentWorld();
            return;
        }

        var resolvedAny = _participants.Any(participant => participant.RetryDeferredResolutions());
        if (resolvedAny)
        {
            RebuildAssignmentsFromResidents();
            _residentService.RestoreResidentsAfterLoad();
            _residentRoutineService.ForceRefreshAllResidents(true);
            _residentRoutineService.ScheduleForcedRefresh(1f, true);
        }
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
            var saveData = _coordinator.Capture(_participants);
            var path = GetSaveFilePath(currentWorldKey);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializer = new XmlSerializer(typeof(WorldSaveEnvelope));
            using (var stream = File.Create(path))
            {
                serializer.Serialize(stream, saveData);
            }

            _log.LogInfo($"Saved modular registry world state to '{path}'. sections={saveData.Sections.Count}.");
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

        foreach (var participant in _participants)
        {
            participant.ResetForWorldChange();
        }
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
            WorldSaveEnvelope? saveData = null;
            var serializer = new XmlSerializer(typeof(WorldSaveEnvelope));
            using (var stream = File.OpenRead(path))
            {
                var deserialized = serializer.Deserialize(stream);
                if (deserialized is WorldSaveEnvelope typedSaveData)
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

            _coordinator.Restore(saveData, _participants);
            RebuildAssignmentsFromResidents();
            _residentService.RestoreResidentsAfterLoad();
            _residentRoutineService.ForceRefreshAllResidents(true);
            _residentRoutineService.ScheduleForcedRefresh(1f, true);
            _hasLoadedCurrentWorld = true;
            _log.LogInfo($"Loaded modular registry world state from '{path}'. sections={saveData.Sections.Count}.");
        }
        catch (Exception exception)
        {
            _hasLoadedCurrentWorld = true;
            _log.LogWarning($"Failed to load registry world state: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private void RebuildAssignmentsFromResidents()
    {
        foreach (var resident in _residentService.RegisteredNpcs)
        {
            foreach (var assignment in resident.Assignments.ToArray())
            {
                if (TryRestoreAssignment(resident, assignment))
                {
                    continue;
                }

                _log.LogWarning($"Resident #{resident.Id} references missing {assignment.Target.TargetKind.ToString().ToLowerInvariant()} #{assignment.Target.TargetId}. Clearing {assignment.Purpose} assignment.");
                resident.ClearAssignment(assignment.Purpose);
                if (assignment.Purpose == ResidentAssignmentPurpose.Work && resident.Role == NpcRole.Innkeeper)
                {
                    resident.SetRole(NpcRole.Villager);
                }
            }
        }
    }

    private bool TryRestoreAssignment(RegisteredNpcData resident, ResidentAssignmentData assignment)
    {
        return assignment.Target.TargetKind switch
        {
            OccupationTargetKind.Slot => _slotService.TryRestoreAssignment(assignment.Target.TargetId, resident.Id),
            OccupationTargetKind.Seat => _seatService.TryRestoreAssignment(assignment.Target.TargetId, resident.Id),
            OccupationTargetKind.Bed => _bedService.TryRestoreAssignment(assignment.Target.TargetId, resident.Id),
            OccupationTargetKind.CraftStation => _craftStationService.TryRestoreAssignment(assignment.Target.TargetId, resident.Id),
            _ => false
        };
    }

    private static string GetCurrentWorldKeyOrEmpty()
    {
        var localPlayer = Player.m_localPlayer;
        var net = ZNet.instance;
        if (localPlayer == null || net == null)
        {
            return string.Empty;
        }

        try
        {
            return SanitizeWorldKey(net.GetWorldName());
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
