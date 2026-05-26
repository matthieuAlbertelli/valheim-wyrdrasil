using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentInteractionService
{
    private readonly ManualLogSource _log;
    private readonly RegistryToolState _toolState;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly ResidentCatalogService _catalogService;
    private readonly ResidentPresenceService _presenceService;
    private readonly ResidentAssignmentService _assignmentService;
    private readonly RegistryResidentTargetingService _targetingService;

    public RegistryResidentInteractionService(
        ManualLogSource log,
        RegistryToolState toolState,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        ResidentRuntimeService runtimeService,
        ResidentCatalogService catalogService,
        ResidentPresenceService presenceService,
        ResidentAssignmentService assignmentService,
        RegistryResidentTargetingService targetingService)
    {
        _log = log;
        _toolState = toolState;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _runtimeService = runtimeService;
        _catalogService = catalogService;
        _presenceService = presenceService;
        _assignmentService = assignmentService;
        _targetingService = targetingService;
    }

    public bool TryGetTargetedRegisteredResident(out RegisteredNpcData resident)
    {
        return _targetingService.TryGetTargetRegisteredResident(out resident);
    }

    public void ForceAssignAtCrosshair()
    {
        if (_targetingService.TryGetTargetRegisteredResident(out var targetedResident))
        {
            _toolState.SetPendingResidentForceAssign(targetedResident.Id, targetedResident.DisplayName);
            _log.LogInfo($"Selected resident #{targetedResident.Id} ('{targetedResident.DisplayName}') for force assignment. Target a compatible anchor to complete the operation.");
            return;
        }

        if (!_toolState.PendingResidentForceAssignId.HasValue || !_catalogService.TryGetResidentById(_toolState.PendingResidentForceAssignId.Value, out var pendingResident))
        {
            _toolState.ClearPendingResidentForceAssign();
            _log.LogWarning("Cannot force assign: target a registered resident first.");
            return;
        }

        var crosshairDescription = _targetingService.DescribeCrosshairTarget();
        _log.LogInfo($"Force assign resolution for resident #{pendingResident.Id}: target={crosshairDescription}.");

        if (_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            _log.LogInfo($"Force assign matched innkeeper slot #{slotData.Id}.");
            if (_assignmentService.TryForceAssignToSlot(pendingResident, slotData))
            {
                _toolState.ClearPendingResidentForceAssign();
                _log.LogInfo($"Force assign completed: resident #{pendingResident.Id} -> slot #{slotData.Id}.");
            }

            return;
        }

        if (_seatService.TryGetSeatAtCrosshair(out var seatData))
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

        if (_bedService.TryGetBedAtCrosshair(out var bedData))
        {
            _log.LogInfo($"Force assign matched bed #{bedData.Id}.");
            if (_assignmentService.TryForceAssignToBed(pendingResident, bedData))
            {
                _toolState.ClearPendingResidentForceAssign();
                _log.LogInfo($"Force assign completed: resident #{pendingResident.Id} -> bed #{bedData.Id}.");
            }

            return;
        }

        if (_craftStationService.TryGetOrDesignateCraftStationAtCrosshair(out var craftStationData, out var craftStationFailureReason))
        {
            _log.LogInfo($"Force assign matched craft station #{craftStationData.Id} ('{craftStationData.DisplayName}').");
            if (_assignmentService.TryForceAssignToCraftStation(pendingResident, craftStationData, out var assignmentFailureReason))
            {
                _toolState.ClearPendingResidentForceAssign();
                _log.LogInfo($"Force assign completed: resident #{pendingResident.Id} -> craft station #{craftStationData.Id}.");
            }
            else
            {
                _log.LogWarning($"Force assign rejected for resident #{pendingResident.Id} on craft station #{craftStationData.Id}: {assignmentFailureReason}");
            }

            return;
        }

        _log.LogWarning($"Cannot force assign resident #{pendingResident.Id}: target an innkeeper slot, a designated seat, a designated bed, a craft station, or another registered resident. Crosshair={crosshairDescription}. CraftStation={craftStationFailureReason}");
    }

    public void ClearTargetInnkeeperSlotAssignmentAtCrosshair()
    {
        if (!_slotService.TryGetSlotAtCrosshair(out var slotData))
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
        if (!_seatService.TryGetSeatAtCrosshair(out var seatData))
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
        if (!_bedService.TryGetBedAtCrosshair(out var bedData))
        {
            _log.LogWarning("Cannot clear bed assignment: no designated bed is under the crosshair.");
            return;
        }

        if (!_assignmentService.TryClearBedAssignment(bedData, out _))
        {
            _log.LogWarning($"Cannot clear bed assignment: bed #{bedData.Id} has no assigned resident.");
        }
    }

    public void DespawnTargetResidentAtCrosshair()
    {
        if (!_targetingService.TryGetTargetRegisteredResident("Cannot despawn resident", out _, out var resident))
        {
            return;
        }

        if (!_presenceService.TryDespawnResident(resident))
        {
            _log.LogWarning($"Cannot despawn resident #{resident.Id}: runtime destruction failed.");
            return;
        }

        _log.LogInfo($"Despawned resident #{resident.Id} ('{resident.DisplayName}').");
    }

    public void RespawnAssignedResidentAtCrosshair()
    {
        if (_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            _presenceService.TryRespawnResidentAssignedToSlot(slotData);
            return;
        }

        if (_seatService.TryGetSeatAtCrosshair(out var seatData))
        {
            _presenceService.TryRespawnResidentAssignedToSeat(seatData);
            return;
        }

        if (_bedService.TryGetBedAtCrosshair(out var bedData))
        {
            _presenceService.TryRespawnResidentAssignedToBed(bedData);
            return;
        }

        if (_craftStationService.TryGetCraftStationAtCrosshair(out var craftStationData))
        {
            _presenceService.TryRespawnResidentAssignedToCraftStation(craftStationData);
            return;
        }

        _log.LogWarning("Cannot respawn resident: the targeted object is neither a registered innkeeper slot, designated seat, designated bed, nor designated craft station.");
    }

    public void ProbeAssignedCraftStationOccupationAtCrosshair()
    {
        if (!_craftStationService.TryGetCraftStationAtCrosshair(out var craftStationData))
        {
            _log.LogWarning("[CraftStation][Probe] Cannot probe occupation: no designated craft station is under the crosshair.");
            return;
        }

        if (!craftStationData.AssignedRegisteredNpcId.HasValue)
        {
            _log.LogWarning($"[CraftStation][Probe] Cannot probe occupation on station #{craftStationData.Id}: no resident is assigned.");
            return;
        }

        if (!_catalogService.TryGetResidentById(craftStationData.AssignedRegisteredNpcId.Value, out var resident))
        {
            _log.LogWarning($"[CraftStation][Probe] Cannot probe occupation on station #{craftStationData.Id}: assigned resident #{craftStationData.AssignedRegisteredNpcId.Value} is missing.");
            return;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            if (!_presenceService.TryRespawnResidentAssignedToCraftStation(craftStationData) ||
                !_runtimeService.TryGetBoundCharacter(resident.Id, out character))
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
        if (!_targetingService.TryGetTargetRegisteredResident("Cannot assign innkeeper role", out var targetCharacter, out var data))
        {
            return;
        }

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
        if (!_targetingService.TryGetTargetRegisteredResident("Cannot assign bed", out var targetCharacter, out var data))
        {
            return;
        }

        if (!_assignmentService.TryAssignBed(data, targetCharacter, out var bedData) || bedData == null)
        {
            _log.LogWarning("Cannot assign bed: no free designated bed is available.");
            return;
        }

        _log.LogInfo($"Assigned designated bed #{bedData.Id} to registered NPC #{data.Id} ('{data.DisplayName}').");
    }

    public bool TryAssignTargetedResidentToConstructionProject(int projectId, out RegisteredNpcData resident, out int workPostId, out string failureReason)
    {
        if (!_targetingService.TryGetTargetRegisteredResident(out resident))
        {
            workPostId = 0;
            failureReason = "Aim at a registered resident to assign them to the construction project.";
            return false;
        }

        return _assignmentService.TryAssignToConstructionProject(resident, projectId, out workPostId, out failureReason);
    }

    public bool TryClearTargetedResidentConstructionAssignment(out RegisteredNpcData resident, out int projectId, out int workPostId, out string failureReason)
    {
        if (!_targetingService.TryGetTargetRegisteredResident(out resident))
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
}
