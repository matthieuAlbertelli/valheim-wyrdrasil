using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.PlayerTool;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Souls.Authoring;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Runtime;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentService
{
    private readonly ManualLogSource _log;
    private readonly RegistryToolState _toolState;
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly ISoulsRuntimeApi _soulsRuntimeApi;
    private readonly ISoulsAuthoringApi _soulsAuthoringApi;
    private readonly ResidentVisualService _visualService;
    private readonly ResidentPresenceService _presenceService;
    private readonly ResidentAssignmentService _assignmentService;
    private readonly ResidentRoutineService _residentRoutineService;
    private readonly IRoutinesRuntimeApi _routinesRuntimeApi;

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _soulsRuntimeApi.RegisteredNpcs;
    public int NextRegisteredNpcId => _soulsRuntimeApi.NextRegisteredNpcId;

    public RegistryResidentService(
        ManualLogSource log,
        RegistryToolState toolState,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        ISettlementsRuntimeApi settlementsRuntimeApi,
        ISoulsRuntimeApi soulsRuntimeApi,
        ISoulsAuthoringApi soulsAuthoringApi,
        IRoutinesRuntimeApi routinesRuntimeApi,
        ResidentVisualService visualService,
        ResidentPresenceService presenceService,
        ResidentAssignmentService assignmentService,
        ResidentRoutineService residentRoutineService)
    {
        _log = log;
        _toolState = toolState;
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _soulsRuntimeApi = soulsRuntimeApi;
        _soulsAuthoringApi = soulsAuthoringApi;
        _routinesRuntimeApi = routinesRuntimeApi;
        _visualService = visualService;
        _presenceService = presenceService;
        _assignmentService = assignmentService;
        _residentRoutineService = residentRoutineService;
    }

    public IReadOnlyDictionary<int, WyrdrasilRegisteredNpcMarker> Markers => _visualService.Markers;

    public void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId)
    {
        _soulsRuntimeApi.LoadResidents(residents, nextResidentId);

        NormalizeResidentsAfterLoad();
    }

    public void NormalizeResidentsAfterLoad()
    {
        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
        {
            NormalizeResidentAfterLoad(resident);
        }

        _visualService.ClearAll();
    }

    public bool TryGetResidentById(int residentId, out RegisteredNpcData resident)
    {
        return _soulsRuntimeApi.TryGetResidentById(residentId, out resident!);
    }

    public bool TryGetTargetedRegisteredResident(out RegisteredNpcData resident)
    {
        return TryGetTargetRegisteredResident(out resident);
    }

    public bool TryGetRegisteredResidentForCharacter(Character character, out RegisteredNpcData resident)
    {
        resident = null!;
        if (character == null)
        {
            return false;
        }

        return _soulsRuntimeApi.TryGetResidentId(character, out var residentId) &&
               _soulsRuntimeApi.TryGetResidentById(residentId, out resident);
    }

    public void PrepareResidentPresenceSnapshotsForSave()
    {
        _presenceService.PrepareResidentPresenceSnapshotsForSave();
    }

    public void RestoreResidentsAfterLoad()
    {
        _presenceService.RestoreResidentsAfterLoad();
    }

    public int ClearStaleConstructionAssignments()
    {
        return _assignmentService.ClearStaleConstructionAssignments();
    }

    public void ClearAllResidents()
    {
        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
        {
            _soulsRuntimeApi.TryDespawnResident(resident.Id);
            resident.PresenceSnapshot.Clear();
        }

        _soulsRuntimeApi.ClearResidents();
        _visualService.ClearAll();
        _toolState.ClearPendingResidentForceAssign();
    }

    public void SetPendingForceAssignResidentVisual(int? residentId)
    {
        _visualService.SetPendingForceAssignResidentVisual(residentId);
    }


    public void SetPendingConstructionAssignmentResidentVisual(int? residentId)
    {
        _visualService.SetPendingConstructionAssignmentResidentVisual(residentId);
    }

    public void RegisterNpcAtCrosshair()
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            _log.LogWarning("Cannot register NPC: no valid character is under the crosshair.");
            return;
        }

        if (!TryRegisterCharacter(targetCharacter, out _, out var failureReason))
        {
            _log.LogWarning(failureReason);
        }
    }

    public bool TrySpawnAndRegisterTestViking(out RegisteredNpcData resident, out string failureReason)
    {
        resident = null!;
        failureReason = string.Empty;

        if (!_soulsAuthoringApi.TrySpawnTestViking(out var spawnedCharacter, out var spawnFailureReason) || spawnedCharacter == null)
        {
            failureReason = spawnFailureReason;
            return false;
        }

        if (!TryRegisterCharacter(spawnedCharacter, out resident, out failureReason))
        {
            return false;
        }

        return true;
    }

    public bool TryKillTargetedRegisteredResident(out RegisteredNpcData resident, out string failureReason)
    {
        resident = null!;
        failureReason = string.Empty;

        if (!TryGetTargetRegisteredResident(out resident))
        {
            failureReason = "No registered resident is under the crosshair.";
            return false;
        }

        _assignmentService.ClearAllAssignmentsForResident(resident);
        _presenceService.TryDespawnResident(resident);
        resident.PresenceSnapshot.Clear();
        _visualService.RemoveMarker(resident.Id);
        _toolState.ClearPendingResidentForceAssign();
        _visualService.SetPendingForceAssignResidentVisual(null);
        _visualService.SetPendingConstructionAssignmentResidentVisual(null);

        if (!_soulsRuntimeApi.RemoveResident(resident.Id))
        {
            failureReason = $"Resident #{resident.Id} could not be removed from the Souls catalog.";
            return false;
        }

        _log.LogInfo($"Killed and unregistered resident #{resident.Id} ('{resident.DisplayName}').");
        return true;
    }

    public bool TryRegisterCharacter(Character targetCharacter, out RegisteredNpcData resident, out string failureReason)
    {
        resident = null!;
        failureReason = string.Empty;

        if (targetCharacter == null)
        {
            failureReason = "Cannot register NPC: target character is null.";
            return false;
        }

        var localPlayer = Player.m_localPlayer;
        if (localPlayer != null && targetCharacter.gameObject == localPlayer.gameObject)
        {
            failureReason = "Cannot register NPC: the local player cannot be registered as a resident.";
            return false;
        }

        if (_soulsRuntimeApi.TryGetResidentId(targetCharacter, out _))
        {
            failureReason = "Cannot register NPC: this character is already registered.";
            return false;
        }

        var displayName = GetCharacterName(targetCharacter);
        var identity = ResolveOrCreateIdentity(targetCharacter, NpcRole.Villager, out var createdIdentity);
        var data = new RegisteredNpcData(_soulsRuntimeApi.AllocateResidentId(), displayName, identity);
        _routinesRuntimeApi.EnsureDefaultAutonomySchedules(data);
        _soulsRuntimeApi.AddResident(data);
        _soulsRuntimeApi.BindResident(data.Id, targetCharacter);
        data.PresenceSnapshot.SetWorldPosition(targetCharacter.transform.position, targetCharacter.transform.eulerAngles.y);
        _visualService.EnsureMarker(data);
        _residentRoutineService.ForceRefreshResident(data);

        resident = data;
        _log.LogInfo($"Registered NPC #{data.Id}: '{data.DisplayName}' with {(createdIdentity ? "new" : "existing")} identity seed={identity.GenerationSeed}, generatedRole={identity.Role}, female={identity.Appearance.IsFemale}.");
        return true;
    }

    public void AssignCraftStationAtCrosshair()
    {
        if (TryGetTargetRegisteredResident(out var targetedResident))
        {
            _toolState.SetPendingResidentForceAssign(targetedResident.Id, targetedResident.DisplayName);
            _log.LogInfo($"Selected resident #{targetedResident.Id} ('{targetedResident.DisplayName}') for craft station assignment. Target a workbench or compatible craft station to complete the operation.");
            return;
        }

        if (!_toolState.PendingResidentForceAssignId.HasValue || !_soulsRuntimeApi.TryGetResidentById(_toolState.PendingResidentForceAssignId.Value, out var pendingResident))
        {
            _toolState.ClearPendingResidentForceAssign();
            _log.LogWarning("Cannot assign craft station: target a registered resident first, then target a workbench or compatible craft station.");
            return;
        }

        var crosshairDescription = DescribeCrosshairTarget();
        if (!_settlementsAuthoringApi.TryGetOrDesignateCraftStationAtCrosshair(out var craftStationData, out var craftStationFailureReason))
        {
            _log.LogWarning($"Cannot assign craft station to resident #{pendingResident.Id}: target a workbench or compatible craft station. Crosshair={crosshairDescription}. CraftStation={craftStationFailureReason}");
            return;
        }

        _log.LogInfo($"Craft station assignment matched station #{craftStationData.Id} ('{craftStationData.DisplayName}') for resident #{pendingResident.Id}.");
        if (_assignmentService.TryAssignToCraftStation(pendingResident, craftStationData))
        {
            _toolState.ClearPendingResidentForceAssign();
            _log.LogInfo($"Craft station assignment completed: resident #{pendingResident.Id} -> craft station #{craftStationData.Id}.");
            return;
        }

        _log.LogWarning($"Craft station assignment rejected for resident #{pendingResident.Id} on craft station #{craftStationData.Id}. The station may already be reserved by a construction project.");
    }

    public void ForceAssignAtCrosshair()
    {
        if (TryGetTargetRegisteredResident(out var targetedResident))
        {
            _toolState.SetPendingResidentForceAssign(targetedResident.Id, targetedResident.DisplayName);
            _log.LogInfo($"Selected resident #{targetedResident.Id} ('{targetedResident.DisplayName}') for force assignment. Target a compatible anchor to complete the operation.");
            return;
        }

        if (!_toolState.PendingResidentForceAssignId.HasValue || !_soulsRuntimeApi.TryGetResidentById(_toolState.PendingResidentForceAssignId.Value, out var pendingResident))
        {
            _toolState.ClearPendingResidentForceAssign();
            _log.LogWarning("Cannot force assign: target a registered resident first.");
            return;
        }

        var crosshairDescription = DescribeCrosshairTarget();
        _log.LogInfo($"Force assign resolution for resident #{pendingResident.Id}: target={crosshairDescription}.");

        if (_settlementsAuthoringApi.TryGetSlotAtCrosshair(out var slotData))
        {
            _log.LogInfo($"Force assign matched innkeeper slot #{slotData.Id}.");
            if (_assignmentService.TryForceAssignToSlot(pendingResident, slotData))
            {
                _toolState.ClearPendingResidentForceAssign();
                _log.LogInfo($"Force assign completed: resident #{pendingResident.Id} -> slot #{slotData.Id}.");
            }

            return;
        }

        if (_settlementsAuthoringApi.TryGetSeatAtCrosshair(out var seatData))
        {
            _log.LogInfo($"Force assign matched seat #{seatData.Id} (usage={seatData.UsageType}).");
            if (_assignmentService.TryForceAssignToSeat(pendingResident, seatData))
            {
                _toolState.ClearPendingResidentForceAssign();
                _log.LogInfo($"Force assign completed: resident #{pendingResident.Id} -> seat #{seatData.Id}.");
            }
            else
            {
                _log.LogWarning("Cannot force assign this seat: public tavern seats are now claimed dynamically at mealtime. Only reserved seats can have an owner.");
            }

            return;
        }

        if (_settlementsAuthoringApi.TryGetBedAtCrosshair(out var bedData))
        {
            _log.LogInfo($"Force assign matched bed #{bedData.Id}.");
            if (_assignmentService.TryForceAssignToBed(pendingResident, bedData))
            {
                _toolState.ClearPendingResidentForceAssign();
                _log.LogInfo($"Force assign completed: resident #{pendingResident.Id} -> bed #{bedData.Id}.");
            }

            return;
        }

        if (_settlementsAuthoringApi.TryGetOrDesignateCraftStationAtCrosshair(out var craftStationData, out var craftStationFailureReason))
        {
            _log.LogInfo($"Force assign matched craft station #{craftStationData.Id} ('{craftStationData.DisplayName}').");
            if (_assignmentService.TryForceAssignToCraftStation(pendingResident, craftStationData))
            {
                _toolState.ClearPendingResidentForceAssign();
                _log.LogInfo($"Force assign completed: resident #{pendingResident.Id} -> craft station #{craftStationData.Id}.");
            }
            else
            {
                _log.LogWarning($"Force assign rejected for resident #{pendingResident.Id} on craft station #{craftStationData.Id}.");
            }

            return;
        }

        _log.LogWarning($"Cannot force assign resident #{pendingResident.Id}: target an innkeeper slot, a designated seat, a designated bed, a craft station, or another registered resident. Crosshair={crosshairDescription}. CraftStation={craftStationFailureReason}");
    }


    private string DescribeCrosshairTarget()
    {
        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return "camera=null";
        }

        var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        if (!Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            return "raycast=none";
        }

        var hitObject = hitInfo.collider != null ? hitInfo.collider.gameObject : null;
        var hitName = hitObject != null ? hitObject.name : "null";
        var station = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<CraftingStation>() : null;
        var chair = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Chair>() : null;
        var bed = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Bed>() : null;
        var character = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Character>() : null;

        return $"hit='{hitName}' point={hitInfo.point} craftingStation={(station != null ? station.gameObject.name : "null")} chair={(chair != null ? chair.gameObject.name : "null")} bed={(bed != null ? bed.gameObject.name : "null")} character={(character != null ? character.gameObject.name : "null")}";
    }

    public void ClearTargetInnkeeperSlotAssignmentAtCrosshair()
    {
        if (!_settlementsAuthoringApi.TryGetSlotAtCrosshair(out var slotData))
        {
            _log.LogWarning("Cannot clear slot assignment: no innkeeper slot is under the crosshair.");
            return;
        }

        if (!_assignmentService.TryClearSlotAssignment(slotData, out _))
        {
            _log.LogWarning($"Cannot clear slot assignment: innkeeper slot #{slotData.Id} has no assigned resident.");
        }
    }

    public void ClearTargetSeatAssignmentAtCrosshair()
    {
        if (!_settlementsAuthoringApi.TryGetSeatAtCrosshair(out var seatData))
        {
            _log.LogWarning("Cannot clear seat assignment: no designated seat is under the crosshair.");
            return;
        }

        if (!_assignmentService.TryClearSeatAssignment(seatData, out _))
        {
            _log.LogWarning($"Cannot clear seat assignment: seat #{seatData.Id} has no assigned resident.");
        }
    }

    public void ClearTargetBedAssignmentAtCrosshair()
    {
        if (!_settlementsAuthoringApi.TryGetBedAtCrosshair(out var bedData))
        {
            _log.LogWarning("Cannot clear bed assignment: no designated bed is under the crosshair.");
            return;
        }

        if (!_assignmentService.TryClearBedAssignment(bedData, out _))
        {
            _log.LogWarning($"Cannot clear bed assignment: bed #{bedData.Id} has no assigned resident.");
        }
    }

    public void ClearTargetCraftStationAssignmentAtCrosshair()
    {
        if (!_settlementsAuthoringApi.TryGetCraftStationAtCrosshair(out var craftStationData))
        {
            _log.LogWarning("Cannot clear craft station assignment: no designated craft station is under the crosshair.");
            return;
        }

        if (!_assignmentService.TryClearCraftStationAssignment(craftStationData, out _))
        {
            _log.LogWarning($"Cannot clear craft station assignment: craft station #{craftStationData.Id} has no assigned resident.");
            return;
        }

        _log.LogInfo($"Cleared craft station assignment on station #{craftStationData.Id} ('{craftStationData.DisplayName}').");
    }

    public void DespawnTargetResidentAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot despawn resident", out _, out var resident)) return;
        if (!_presenceService.TryDespawnResident(resident))
        {
            _log.LogWarning($"Cannot despawn resident #{resident.Id}: runtime destruction failed.");
            return;
        }
        _log.LogInfo($"Despawned resident #{resident.Id} ('{resident.DisplayName}').");
    }

    public void RespawnAssignedResidentAtCrosshair()
    {
        if (_settlementsAuthoringApi.TryGetSlotAtCrosshair(out var slotData))
        {
            _presenceService.TryRespawnResidentAssignedToSlot(slotData);
            return;
        }

        if (_settlementsAuthoringApi.TryGetSeatAtCrosshair(out var seatData))
        {
            _presenceService.TryRespawnResidentAssignedToSeat(seatData);
            return;
        }

        if (_settlementsAuthoringApi.TryGetBedAtCrosshair(out var bedData))
        {
            _presenceService.TryRespawnResidentAssignedToBed(bedData);
            return;
        }

        if (_settlementsAuthoringApi.TryGetCraftStationAtCrosshair(out var craftStationData))
        {
            _presenceService.TryRespawnResidentAssignedToCraftStation(craftStationData);
            return;
        }

        _log.LogWarning("Cannot respawn resident: the targeted object is neither a registered innkeeper slot, designated seat, designated bed, nor designated craft station.");
    }


    public void ProbeAssignedCraftStationOccupationAtCrosshair()
    {
        if (!_settlementsAuthoringApi.TryGetCraftStationAtCrosshair(out var craftStationData))
        {
            _log.LogWarning("[CraftStation][Probe] Cannot probe occupation: no designated craft station is under the crosshair.");
            return;
        }

        if (!craftStationData.AssignedRegisteredNpcId.HasValue)
        {
            _log.LogWarning($"[CraftStation][Probe] Cannot probe occupation on station #{craftStationData.Id}: no resident is assigned.");
            return;
        }

        if (!_soulsRuntimeApi.TryGetResidentById(craftStationData.AssignedRegisteredNpcId.Value, out var resident))
        {
            _log.LogWarning($"[CraftStation][Probe] Cannot probe occupation on station #{craftStationData.Id}: assigned resident #{craftStationData.AssignedRegisteredNpcId.Value} is missing.");
            return;
        }

        if (!_soulsRuntimeApi.TryGetBoundCharacter(resident.Id, out var character))
        {
            if (!_presenceService.TryRespawnResidentAssignedToCraftStation(craftStationData) ||
                !_soulsRuntimeApi.TryGetBoundCharacter(resident.Id, out character))
            {
                _log.LogWarning($"[CraftStation][Probe] Cannot probe occupation on station #{craftStationData.Id}: resident #{resident.Id} could not be spawned.");
                return;
            }
        }

        if (!craftStationData.TryResolveWorldAnchor(out var anchorWorldPosition, out var anchorWorldForward))
        {
            _log.LogWarning($"[CraftStation][Probe] Cannot probe occupation on station #{craftStationData.Id}: world anchor resolution failed.");
            return;
        }

        var actorFacing = GetCraftStationActorFacing(anchorWorldForward);
        character.transform.position = anchorWorldPosition;
        character.transform.rotation = Quaternion.LookRotation(actorFacing, Vector3.up);

        if (character.TryGetComponent<WyrdrasilVikingNpcAI>(out var ai))
        {
            ai.ClearSteering();
            ai.SetCivilianWalkLocomotion(false);
        }

        if (character.TryGetComponent<WyrdrasilRouteTraversalController>(out var routeTraversalController))
        {
            routeTraversalController.ReleaseControl();
        }

        if (character.TryGetComponent<Rigidbody>(out var rigidbody))
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        var enteredPose = false;
        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
            enteredPose = viking.IsInWorkbenchPose() || viking.TryEnterWorkbenchPose();
        }

        _log.LogInfo($"[CraftStation][Probe] Snapped resident #{resident.Id} ('{resident.DisplayName}') to station #{craftStationData.Id}. anchor={anchorWorldPosition} actorFacing={actorFacing} enteredPose={enteredPose}.");
    }

    public void AssignInnkeeperRoleAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot assign innkeeper role", out var targetCharacter, out var data)) return;
        if (!_assignmentService.TryAssignInnkeeperRole(data, targetCharacter, out var slotData) || slotData == null)
        {
            _log.LogWarning("Cannot assign innkeeper role: no free Innkeeper slot is available.");
            return;
        }
        _log.LogInfo($"Assigned Innkeeper role to registered NPC #{data.Id} ('{data.DisplayName}') using slot #{slotData.Id}.");
    }

    public void AssignSeatAtCrosshair()
    {
        _log.LogWarning("Seat ownership is disabled for tavern seating in this iteration. Public seats are now claimed dynamically at mealtime.");
    }

    public void AssignBedAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot assign bed", out var targetCharacter, out var data)) return;
        if (!_assignmentService.TryAssignBed(data, targetCharacter, out var bedData) || bedData == null)
        {
            _log.LogWarning("Cannot assign bed: no free designated bed is available.");
            return;
        }
        _log.LogInfo($"Assigned designated bed #{bedData.Id} to registered NPC #{data.Id} ('{data.DisplayName}').");
    }

    public bool TryAssignTargetedResidentToConstructionProject(int projectId, out RegisteredNpcData resident, out int workPostId, out string failureReason)
    {
        if (!TryGetTargetRegisteredResident(out resident))
        {
            workPostId = 0;
            failureReason = "Aim at a registered resident to assign them to the construction project.";
            return false;
        }

        return TryAssignResidentToConstructionProject(resident, projectId, out workPostId, out failureReason);
    }

    public bool TryAssignResidentToConstructionProject(RegisteredNpcData resident, int projectId, out int workPostId, out string failureReason)
    {
        workPostId = 0;
        if (resident == null)
        {
            failureReason = "Resident is null.";
            return false;
        }

        return _assignmentService.TryAssignToConstructionProject(resident, projectId, out workPostId, out failureReason);
    }

    public bool TryClearTargetedResidentConstructionAssignment(out RegisteredNpcData resident, out int projectId, out int workPostId, out string failureReason)
    {
        if (!TryGetTargetRegisteredResident(out resident))
        {
            projectId = 0;
            workPostId = 0;
            failureReason = "Aim at a registered resident to clear their construction assignment.";
            return false;
        }

        return _assignmentService.TryClearConstructionAssignment(resident, out projectId, out workPostId, out failureReason);
    }


    private static Vector3 GetCraftStationActorFacing(Vector3 anchorForward)
    {
        anchorForward.y = 0f;
        if (anchorForward.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return -anchorForward.normalized;
    }

    private void NormalizeResidentAfterLoad(RegisteredNpcData resident)
    {
        _routinesRuntimeApi.EnsureDefaultAutonomySchedules(resident);

        if (!resident.AssignedSeatId.HasValue)
        {
            return;
        }

        if (!_settlementsRuntimeApi.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
        {
            return;
        }

        if (seatData.UsageType != SeatUsageType.Public)
        {
            return;
        }

        resident.ClearAssignedSeat();
        _routinesRuntimeApi.ClearAssignedSeatSchedule(resident);

        if (resident.PresenceSnapshot.IsAssignedTargetAnchor(ResidentAssignmentPurpose.Meal))
        {
            resident.PresenceSnapshot.SetWorldPosition(
                resident.PresenceSnapshot.WorldPosition,
                resident.PresenceSnapshot.WorldYawDegrees);
        }

        _log.LogInfo($"Migrated resident #{resident.Id} away from legacy public seat ownership. Public tavern seats are now claimed dynamically at mealtime.");
    }

    public void HandleDeletedSlot(int slotId)
    {
        _assignmentService.HandleDeletedSlot(slotId);
    }

    public void HandleDeletedSeat(int seatId)
    {
        _assignmentService.HandleDeletedSeat(seatId);
    }

    public void HandleDeletedBed(int bedId)
    {
        _assignmentService.HandleDeletedBed(bedId);
    }

    public void HandleDeletedCraftStation(int craftStationId)
    {
        _assignmentService.HandleDeletedCraftStation(craftStationId);
    }

    private bool TryGetTargetCharacter(out Character targetCharacter)
    {
        return RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out targetCharacter);
    }

    private bool TryGetTargetRegisteredResident(out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            resident = null!;
            return false;
        }

        return TryGetRegisteredResidentForCharacter(targetCharacter, out resident);
    }

    private bool TryGetTargetRegisteredResident(string actionLabel, out Character targetCharacter, out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out targetCharacter))
        {
            _log.LogWarning($"{actionLabel}: no valid character is under the crosshair.");
            resident = null!;
            return false;
        }

        if (!TryGetRegisteredResidentForCharacter(targetCharacter, out resident))
        {
            _log.LogWarning($"{actionLabel}: the targeted character is not registered.");
            resident = null!;
            return false;
        }

        return true;
    }

    private string GetCharacterName(Character character)
    {
        var nameField = typeof(Character).GetField("m_name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return nameField?.GetValue(character) as string ?? character.gameObject.name;
    }

    private VikingIdentityData ResolveOrCreateIdentity(Character targetCharacter, NpcRole defaultRole, out bool createdIdentity)
    {
        return _soulsAuthoringApi.ResolveOrCreateIdentity(targetCharacter, defaultRole, out createdIdentity);
    }

}
