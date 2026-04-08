using Wyrdrasil.Registry.Components;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentPresenceService
{
    private readonly RegistryResidentCatalogService _catalogService;
    private readonly RegistryResidentRuntimeService _runtimeService;
    private readonly RegistrySpawnService _spawnService;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryBedService _bedService;
    private readonly RegistryResidentOccupationService _occupationService;
    private readonly RegistryResidentVisualService _visualService;

    public RegistryResidentPresenceService(
        RegistryResidentCatalogService catalogService,
        RegistryResidentRuntimeService runtimeService,
        RegistrySpawnService spawnService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryBedService bedService,
        RegistryResidentOccupationService occupationService,
        RegistryResidentVisualService visualService)
    {
        _catalogService = catalogService;
        _runtimeService = runtimeService;
        _spawnService = spawnService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _occupationService = occupationService;
        _visualService = visualService;
    }

    public void PrepareResidentPresenceSnapshotsForSave()
    {
        foreach (var resident in _catalogService.RegisteredNpcs)
        {
            if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
            {
                continue;
            }

            if (!_runtimeService.TryCaptureBoundResidentTransform(resident.Id, out var worldPosition, out var worldYawDegrees, out var isAttached))
            {
                continue;
            }

            if (IsAttachedToAssignedBed(resident))
            {
                resident.PresenceSnapshot.SetAssignedBedAnchor(worldPosition, worldYawDegrees);
                continue;
            }

            if (IsAttachedToAssignedSeat(resident))
            {
                resident.PresenceSnapshot.SetAssignedSeatAnchor(worldPosition, worldYawDegrees);
                continue;
            }

            if (resident.AssignedSlotId.HasValue && isAttached)
            {
                resident.PresenceSnapshot.SetAssignedSlotAnchor(worldPosition, worldYawDegrees);
                continue;
            }

            resident.PresenceSnapshot.SetWorldPosition(worldPosition, worldYawDegrees);
        }
    }

    public void RestoreResidentsAfterLoad()
    {
        foreach (var resident in _catalogService.RegisteredNpcs)
        {
            if (!resident.PresenceSnapshot.ShouldRespawnOnLoad)
            {
                continue;
            }

            if (_runtimeService.TryGetBoundCharacter(resident.Id, out _))
            {
                continue;
            }

            if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
            {
                continue;
            }

            switch (resident.PresenceSnapshot.RestoreMode)
            {
                case ResidentRestoreMode.WorldPosition:
                    RestoreResidentAtWorldPosition(resident);
                    break;

                case ResidentRestoreMode.AssignedSeatAnchor:
                    RestoreResidentAtAssignedSeat(resident);
                    break;

                case ResidentRestoreMode.AssignedSlotAnchor:
                    RestoreResidentAtAssignedSlot(resident);
                    break;

                case ResidentRestoreMode.AssignedBedAnchor:
                    RestoreResidentAtAssignedBed(resident);
                    break;
            }
        }
    }

    public bool TryDespawnResident(RegisteredNpcData resident)
    {
        if (!_runtimeService.TryDespawnResident(resident.Id))
        {
            return false;
        }

        resident.PresenceSnapshot.Clear();
        return true;
    }

    public bool TryRespawnResidentAssignedToSlot(ZoneSlotData slotData)
    {
        if (!slotData.AssignedRegisteredNpcId.HasValue || !_catalogService.TryGetResidentById(slotData.AssignedRegisteredNpcId.Value, out var resident))
        {
            return false;
        }

        if (_runtimeService.TryGetBoundCharacter(resident.Id, out _))
        {
            return false;
        }

        if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
        {
            return false;
        }

        var spawnPosition = slotData.Position;
        var spawnRotation = BuildFacingRotation(spawnPosition, GetPlayerLookTargetOrFallback(spawnPosition));
        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _))
        {
            return false;
        }

        _occupationService.TryOccupyAssignedSlot(resident);
        return true;
    }

    public bool TryRespawnResidentAssignedToSeat(RegisteredSeatData seatData)
    {
        if (!seatData.AssignedRegisteredNpcId.HasValue || !_catalogService.TryGetResidentById(seatData.AssignedRegisteredNpcId.Value, out var resident))
        {
            return false;
        }

        if (_runtimeService.TryGetBoundCharacter(resident.Id, out _))
        {
            return false;
        }

        if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
        {
            return false;
        }

        var spawnPosition = seatData.ApproachPosition;
        var spawnRotation = BuildFacingRotation(spawnPosition, seatData.SeatPosition);
        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _))
        {
            return false;
        }

        _occupationService.TryOccupyAssignedSeat(resident);
        return true;
    }

    public bool TryRespawnResidentAssignedToBed(RegisteredBedData bedData)
    {
        if (!bedData.AssignedRegisteredNpcId.HasValue || !_catalogService.TryGetResidentById(bedData.AssignedRegisteredNpcId.Value, out var resident))
        {
            return false;
        }

        if (_runtimeService.TryGetBoundCharacter(resident.Id, out _))
        {
            return false;
        }

        if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
        {
            return false;
        }

        var spawnPosition = bedData.ApproachPosition;
        var spawnRotation = BuildFacingRotation(spawnPosition, bedData.SleepPosition);
        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _))
        {
            return false;
        }

        _occupationService.TryOccupyAssignedBed(resident);
        return true;
    }

    private bool IsAttachedToAssignedSeat(RegisteredNpcData resident)
    {
        if (!resident.AssignedSeatId.HasValue)
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (character is not WyrdrasilVikingNpc viking)
        {
            return false;
        }

        if (!_seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData) || seatData.ChairComponent == null)
        {
            return false;
        }

        return viking.IsAttachedToChair(seatData.ChairComponent);
    }

    private bool IsAttachedToAssignedBed(RegisteredNpcData resident)
    {
        if (!resident.AssignedBedId.HasValue)
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (character is not WyrdrasilVikingNpc viking)
        {
            return false;
        }

        if (!_bedService.TryGetBedById(resident.AssignedBedId.Value, out var bedData) || bedData.BedComponent == null)
        {
            return false;
        }

        return viking.IsAttachedToBed(bedData.BedComponent);
    }

    private void RestoreResidentAtWorldPosition(RegisteredNpcData resident)
    {
        var spawnPosition = resident.PresenceSnapshot.WorldPosition;
        var spawnRotation = Quaternion.Euler(0f, resident.PresenceSnapshot.WorldYawDegrees, 0f);
        TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _);
    }

    private void RestoreResidentAtAssignedSeat(RegisteredNpcData resident)
    {
        if (!resident.AssignedSeatId.HasValue)
        {
            return;
        }

        if (!_seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
        {
            return;
        }

        TryRespawnResidentAssignedToSeat(seatData);
    }

    private void RestoreResidentAtAssignedSlot(RegisteredNpcData resident)
    {
        if (!resident.AssignedSlotId.HasValue)
        {
            return;
        }

        if (!_slotService.TryGetSlotById(resident.AssignedSlotId.Value, out var slotData))
        {
            return;
        }

        TryRespawnResidentAssignedToSlot(slotData);
    }

    private void RestoreResidentAtAssignedBed(RegisteredNpcData resident)
    {
        if (!resident.AssignedBedId.HasValue)
        {
            return;
        }

        if (!_bedService.TryGetBedById(resident.AssignedBedId.Value, out var bedData))
        {
            return;
        }

        TryRespawnResidentAssignedToBed(bedData);
    }

    private bool TrySpawnAndBindResident(RegisteredNpcData resident, Vector3 spawnPosition, Quaternion spawnRotation, out Character runtimeCharacter)
    {
        runtimeCharacter = null!;
        _runtimeService.MarkResidentSpawning(resident.Id);
        if (!_spawnService.TrySpawnResident(resident, spawnPosition, spawnRotation, out var instance) || instance == null)
        {
            _runtimeService.MarkResidentMissing(resident.Id);
            return false;
        }

        var character = instance.GetComponent<Character>();
        if (character == null)
        {
            Object.Destroy(instance);
            _runtimeService.MarkResidentMissing(resident.Id);
            return false;
        }

        _runtimeService.BindResident(resident.Id, character);
        resident.PresenceSnapshot.SetWorldPosition(spawnPosition, spawnRotation.eulerAngles.y);
        _visualService.EnsureMarker(resident);
        runtimeCharacter = character;
        return true;
    }

    private static Quaternion BuildFacingRotation(Vector3 fromPosition, Vector3 toPosition)
    {
        var direction = toPosition - fromPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.forward;
        }

        return Quaternion.LookRotation(direction.normalized);
    }

    private static Vector3 GetPlayerLookTargetOrFallback(Vector3 fallbackPosition)
    {
        var localPlayer = Player.m_localPlayer;
        return localPlayer ? localPlayer.transform.position : fallbackPosition + Vector3.forward;
    }
}
