using System.Linq;
using Wyrdrasil.Registry.Components;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class ResidentPresenceService
{
    private readonly ResidentCatalogService _catalogService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly NpcSpawnService _spawnService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly NavigationWaypointService _waypointService;
    private readonly ResidentOccupationService _occupationService;
    private readonly ResidentVisualService _visualService;

    public ResidentPresenceService(
        ResidentCatalogService catalogService,
        ResidentRuntimeService runtimeService,
        NpcSpawnService spawnService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        NavigationWaypointService waypointService,
        ResidentOccupationService occupationService,
        ResidentVisualService visualService)
    {
        _catalogService = catalogService;
        _runtimeService = runtimeService;
        _spawnService = spawnService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _waypointService = waypointService;
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

            if (!_runtimeService.TryCaptureBoundResidentTransform(
                    resident.Id,
                    out var worldPosition,
                    out var worldYawDegrees,
                    out var isAttached))
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

            if (TryGetPublicSeatRestoreAnchor(resident, out var publicSeatRestorePosition, out var publicSeatRestoreYawDegrees))
            {
                resident.PresenceSnapshot.SetWorldPosition(publicSeatRestorePosition, publicSeatRestoreYawDegrees);
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
        _occupationService.ReleaseOccupation(resident);

        if (!_runtimeService.TryDespawnResident(resident.Id))
        {
            return false;
        }

        resident.PresenceSnapshot.Clear();
        return true;
    }

    public bool TryRespawnResidentAssignedToSlot(ZoneSlotData slotData)
    {
        if (!slotData.AssignedRegisteredNpcId.HasValue ||
            !_catalogService.TryGetResidentById(slotData.AssignedRegisteredNpcId.Value, out var resident))
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

        _occupationService.TryStartOccupation(resident, ResidentRoutineActivityType.WorkAtAssignedSlot);
        return true;
    }

    public bool TryRespawnResidentAssignedToSeat(RegisteredSeatData seatData)
    {
        if (!seatData.AssignedRegisteredNpcId.HasValue ||
            !_catalogService.TryGetResidentById(seatData.AssignedRegisteredNpcId.Value, out var resident))
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

        _occupationService.TryStartOccupation(resident, ResidentRoutineActivityType.SitAtAssignedSeat);
        return true;
    }

    public bool TryRespawnResidentAssignedToBed(RegisteredBedData bedData)
    {
        if (!bedData.AssignedRegisteredNpcId.HasValue ||
            !_catalogService.TryGetResidentById(bedData.AssignedRegisteredNpcId.Value, out var resident))
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

        _occupationService.TryStartOccupation(resident, ResidentRoutineActivityType.SleepAtAssignedBed);
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

    private bool TryGetPublicSeatRestoreAnchor(RegisteredNpcData resident, out Vector3 worldPosition, out float worldYawDegrees)
    {
        worldPosition = Vector3.zero;
        worldYawDegrees = 0f;

        if (!_seatService.TryGetOccupiedSeatForResident(resident.Id, out var seatData))
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (character is not WyrdrasilVikingNpc viking ||
            seatData.ChairComponent == null ||
            !viking.IsAttachedToChair(seatData.ChairComponent))
        {
            return false;
        }

        worldPosition = seatData.ApproachPosition;
        var rotation = BuildFacingRotation(seatData.ApproachPosition, seatData.SeatPosition);
        worldYawDegrees = rotation.eulerAngles.y;
        return true;
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
        var spawnPosition = ResolveSafeWorldSpawnPosition(resident.PresenceSnapshot.WorldPosition);
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

    private Vector3 ResolveSafeWorldSpawnPosition(Vector3 preferredPosition)
    {
        if (TryGetNearestWaypointPosition(preferredPosition, 12f, out var nearbyWaypointPosition) &&
            TryResolveProjectedSpawnPosition(nearbyWaypointPosition, out var safeWaypointPosition))
        {
            return safeWaypointPosition;
        }

        if (TryResolveProjectedSpawnPosition(preferredPosition, out var projectedPreferredPosition))
        {
            return projectedPreferredPosition;
        }

        foreach (var offset in GetSpawnSearchOffsets())
        {
            if (TryResolveProjectedSpawnPosition(preferredPosition + offset, out var offsetResolvedPosition))
            {
                return offsetResolvedPosition;
            }
        }

        if (TryGetNearestWaypointPosition(preferredPosition, float.MaxValue, out var fallbackWaypointPosition) &&
            TryResolveProjectedSpawnPosition(fallbackWaypointPosition, out var fallbackResolvedPosition))
        {
            return fallbackResolvedPosition;
        }

        return preferredPosition;
    }

    private bool TryGetNearestWaypointPosition(Vector3 origin, float maxHorizontalDistance, out Vector3 waypointPosition)
    {
        waypointPosition = Vector3.zero;

        var nearestWaypoint = _waypointService.Waypoints
            .OrderBy(candidate => HorizontalDistance(origin, candidate.Position))
            .FirstOrDefault();

        if (nearestWaypoint == null)
        {
            return false;
        }

        if (HorizontalDistance(origin, nearestWaypoint.Position) > maxHorizontalDistance)
        {
            return false;
        }

        waypointPosition = nearestWaypoint.Position;
        return true;
    }

    private static bool TryResolveProjectedSpawnPosition(Vector3 candidatePosition, out Vector3 resolvedPosition)
    {
        var rayOrigin = candidatePosition + Vector3.up * 3f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hitInfo, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            resolvedPosition = hitInfo.point + Vector3.up * 0.05f;
            return true;
        }

        resolvedPosition = Vector3.zero;
        return false;
    }

    private static Vector3[] GetSpawnSearchOffsets()
    {
        return new[]
        {
            Vector3.zero,
            new Vector3(0.90f, 0f, 0f),
            new Vector3(-0.90f, 0f, 0f),
            new Vector3(0f, 0f, 0.90f),
            new Vector3(0f, 0f, -0.90f),
            new Vector3(1.60f, 0f, 0f),
            new Vector3(-1.60f, 0f, 0f),
            new Vector3(0f, 0f, 1.60f),
            new Vector3(0f, 0f, -1.60f),
            new Vector3(1.10f, 0f, 1.10f),
            new Vector3(1.10f, 0f, -1.10f),
            new Vector3(-1.10f, 0f, 1.10f),
            new Vector3(-1.10f, 0f, -1.10f)
        };
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var delta = b - a;
        delta.y = 0f;
        return delta.magnitude;
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