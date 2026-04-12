using System.Linq;
using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Occupations;
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
    private readonly SeatService _seatService;
    private readonly NavigationWaypointService _waypointService;
    private readonly ResidentOccupationService _occupationService;
    private readonly OccupationResolverRegistry _occupationResolverRegistry;
    private readonly ResidentVisualService _visualService;

    public ResidentPresenceService(
        ResidentCatalogService catalogService,
        ResidentRuntimeService runtimeService,
        NpcSpawnService spawnService,
        SeatService seatService,
        NavigationWaypointService waypointService,
        ResidentOccupationService occupationService,
        OccupationResolverRegistry occupationResolverRegistry,
        ResidentVisualService visualService)
    {
        _catalogService = catalogService;
        _runtimeService = runtimeService;
        _spawnService = spawnService;
        _seatService = seatService;
        _waypointService = waypointService;
        _occupationService = occupationService;
        _occupationResolverRegistry = occupationResolverRegistry;
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

            if (TryCaptureAssignedTargetAnchor(resident, ResidentAssignmentPurpose.Sleep, isAttached, worldPosition, worldYawDegrees) ||
                TryCaptureAssignedTargetAnchor(resident, ResidentAssignmentPurpose.Meal, isAttached, worldPosition, worldYawDegrees))
            {
                continue;
            }

            if (TryGetPublicSeatRestoreAnchor(resident, out var publicSeatRestorePosition, out var publicSeatRestoreYawDegrees))
            {
                resident.PresenceSnapshot.SetWorldPosition(publicSeatRestorePosition, publicSeatRestoreYawDegrees);
                continue;
            }

            if (TryCaptureAssignedTargetAnchor(resident, ResidentAssignmentPurpose.Work, isAttached, worldPosition, worldYawDegrees))
            {
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

                case ResidentRestoreMode.AssignedTargetAnchor:
                    if (resident.PresenceSnapshot.AssignedPurpose.HasValue)
                    {
                        RestoreResidentAtAssignedTarget(resident, resident.PresenceSnapshot.AssignedPurpose.Value);
                    }

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
        return TryRespawnResidentAssignedToTarget(slotData.AssignedRegisteredNpcId, ResidentAssignmentPurpose.Work);
    }

    public bool TryRespawnResidentAssignedToSeat(RegisteredSeatData seatData)
    {
        return TryRespawnResidentAssignedToTarget(seatData.AssignedRegisteredNpcId, ResidentAssignmentPurpose.Meal);
    }

    public bool TryRespawnResidentAssignedToBed(RegisteredBedData bedData)
    {
        return TryRespawnResidentAssignedToTarget(bedData.AssignedRegisteredNpcId, ResidentAssignmentPurpose.Sleep);
    }

    public bool TryRespawnResidentAssignedToCraftStation(RegisteredCraftStationData craftStationData)
    {
        return TryRespawnResidentAssignedToTarget(craftStationData.AssignedRegisteredNpcId, ResidentAssignmentPurpose.Work);
    }

    private bool TryCaptureAssignedTargetAnchor(
        RegisteredNpcData resident,
        ResidentAssignmentPurpose purpose,
        bool isAttached,
        Vector3 worldPosition,
        float worldYawDegrees)
    {
        if (!TryResolveAssignedOccupationTarget(resident, purpose, out _, out var target) ||
            !IsResidentUsingAssignedTarget(resident, target, isAttached))
        {
            return false;
        }

        resident.PresenceSnapshot.SetAssignedTargetAnchor(purpose, worldPosition, worldYawDegrees);
        return true;
    }

    private bool TryRespawnResidentAssignedToTarget(int? residentId, ResidentAssignmentPurpose purpose)
    {
        if (!residentId.HasValue || !_catalogService.TryGetResidentById(residentId.Value, out var resident))
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

        return RestoreResidentAtAssignedTarget(resident, purpose);
    }

    private bool IsResidentUsingAssignedTarget(RegisteredNpcData resident, OccupationTarget target, bool isAttached)
    {
        if (target.Execution.IsStand)
        {
            return isAttached;
        }

        if (target.Execution.IsSeat)
        {
            return TryGetBoundViking(resident, out var seatViking) &&
                   target.Execution.ChairComponent != null &&
                   seatViking.IsAttachedToChair(target.Execution.ChairComponent);
        }

        if (target.Execution.IsBed)
        {
            return TryGetBoundViking(resident, out var bedViking) &&
                   target.Execution.BedComponent != null &&
                   bedViking.IsAttachedToBed(target.Execution.BedComponent);
        }

        return false;
    }

    private bool TryGetBoundViking(RegisteredNpcData resident, out WyrdrasilVikingNpc viking)
    {
        if (_runtimeService.TryGetBoundCharacter(resident.Id, out var character) && character is WyrdrasilVikingNpc typedViking)
        {
            viking = typedViking;
            return true;
        }

        viking = null!;
        return false;
    }

    private bool TryGetPublicSeatRestoreAnchor(RegisteredNpcData resident, out Vector3 worldPosition, out float worldYawDegrees)
    {
        worldPosition = Vector3.zero;
        worldYawDegrees = 0f;

        if (!_seatService.TryGetOccupiedSeatForResident(resident.Id, out var seatData))
        {
            return false;
        }

        if (!TryGetBoundViking(resident, out var viking) ||
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

    private void RestoreResidentAtWorldPosition(RegisteredNpcData resident)
    {
        var spawnPosition = ResolveSafeWorldSpawnPosition(resident.PresenceSnapshot.WorldPosition);
        var spawnRotation = Quaternion.Euler(0f, resident.PresenceSnapshot.WorldYawDegrees, 0f);
        TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _);
    }

    private bool RestoreResidentAtAssignedTarget(RegisteredNpcData resident, ResidentAssignmentPurpose purpose)
    {
        if (!TryResolveAssignedOccupationTarget(resident, purpose, out var activityType, out var target))
        {
            return false;
        }

        var spawnPosition = target.Plan.ApproachPosition;
        var lookTarget = target.Plan.EngagePosition;
        if (target.Execution.IsStand && (lookTarget - spawnPosition).sqrMagnitude <= 0.0001f)
        {
            lookTarget = GetPlayerLookTargetOrFallback(spawnPosition);
        }

        var spawnRotation = BuildFacingRotation(spawnPosition, lookTarget);
        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _))
        {
            return false;
        }

        _occupationService.TryStartOccupation(resident, activityType);
        return true;
    }

    private bool TryResolveAssignedOccupationTarget(
        RegisteredNpcData resident,
        ResidentAssignmentPurpose purpose,
        out ResidentRoutineActivityType activityType,
        out OccupationTarget target)
    {
        if (!TryGetAssignedActivityType(resident, purpose, out activityType) ||
            !_occupationResolverRegistry.TryGetResolver(activityType, out var resolver) ||
            !resolver.TryResolve(resident, out target))
        {
            activityType = default;
            target = null!;
            return false;
        }

        return true;
    }

    private static bool TryGetAssignedActivityType(RegisteredNpcData resident, ResidentAssignmentPurpose purpose, out ResidentRoutineActivityType activityType)
    {
        switch (purpose)
        {
            case ResidentAssignmentPurpose.Work:
                if (resident.TryGetAssignedTarget(ResidentAssignmentPurpose.Work, out var workTarget))
                {
                    switch (workTarget.TargetKind)
                    {
                        case OccupationTargetKind.Slot:
                            activityType = ResidentRoutineActivityType.WorkAtAssignedSlot;
                            return true;
                        case OccupationTargetKind.CraftStation:
                            activityType = ResidentRoutineActivityType.WorkAtAssignedCraftStation;
                            return true;
                    }
                }
                break;
            case ResidentAssignmentPurpose.Meal:
                activityType = ResidentRoutineActivityType.SitAtAssignedSeat;
                return true;
            case ResidentAssignmentPurpose.Sleep:
                activityType = ResidentRoutineActivityType.SleepAtAssignedBed;
                return true;
        }

        activityType = default;
        return false;
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
