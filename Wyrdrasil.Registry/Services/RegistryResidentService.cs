using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentService
{
    private readonly ManualLogSource _log;
    private readonly RegistryToolState _toolState;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly NpcIdentityGenerator _identityGenerator;
    private readonly NpcCustomizationApplier _customizationApplier;
    private readonly ResidentCatalogService _catalogService;
    private readonly ResidentVisualService _visualService;
    private readonly ResidentPresenceService _presenceService;
    private readonly ResidentAssignmentService _assignmentService;
    private readonly ResidentScheduleService _scheduleService;

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _catalogService.RegisteredNpcs;
    public int NextRegisteredNpcId => _catalogService.NextRegisteredNpcId;

    public RegistryResidentService(
        ManualLogSource log,
        RegistryToolState toolState,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        ResidentRuntimeService runtimeService,
        NpcIdentityGenerator identityGenerator,
        NpcCustomizationApplier customizationApplier,
        ResidentCatalogService catalogService,
        ResidentVisualService visualService,
        ResidentPresenceService presenceService,
        ResidentAssignmentService assignmentService,
        ResidentScheduleService scheduleService)
    {
        _log = log;
        _toolState = toolState;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _runtimeService = runtimeService;
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
        _catalogService = catalogService;
        _visualService = visualService;
        _presenceService = presenceService;
        _assignmentService = assignmentService;
        _scheduleService = scheduleService;
    }

    public IReadOnlyDictionary<int, WyrdrasilRegisteredNpcMarker> Markers => _visualService.Markers;

    public void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId)
    {
        _catalogService.LoadResidents(residents, nextResidentId);

        foreach (var resident in _catalogService.RegisteredNpcs)
        {
            NormalizeResidentAfterLoad(resident);
        }

        _visualService.ClearAll();
    }

    public bool TryGetResidentById(int residentId, out RegisteredNpcData resident)
    {
        return _catalogService.TryGetResidentById(residentId, out resident!);
    }

    public void PrepareResidentPresenceSnapshotsForSave()
    {
        _presenceService.PrepareResidentPresenceSnapshotsForSave();
    }

    public void RestoreResidentsAfterLoad()
    {
        _presenceService.RestoreResidentsAfterLoad();
    }

    public void ClearAllResidents()
    {
        foreach (var resident in _catalogService.RegisteredNpcs)
        {
            _runtimeService.TryDespawnResident(resident.Id);
            resident.PresenceSnapshot.Clear();
        }

        _catalogService.Clear();
        _visualService.ClearAll();
        _toolState.ClearPendingResidentForceAssign();
    }

    public void SetPendingForceAssignResidentVisual(int? residentId)
    {
        _visualService.SetPendingForceAssignResidentVisual(residentId);
    }

    public void RegisterNpcAtCrosshair()
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            _log.LogWarning("Cannot register NPC: no valid character is under the crosshair.");
            return;
        }

        var localPlayer = Player.m_localPlayer;
        if (localPlayer != null && targetCharacter.gameObject == localPlayer.gameObject)
        {
            _log.LogWarning("Cannot register NPC: the local player cannot be registered as a resident.");
            return;
        }

        if (_runtimeService.TryGetResidentId(targetCharacter, out _))
        {
            _log.LogWarning("Cannot register NPC: this character is already registered.");
            return;
        }

        var displayName = GetCharacterName(targetCharacter);
        var identity = ResolveOrCreateIdentity(targetCharacter, NpcRole.Villager, out var createdIdentity);
        var data = new RegisteredNpcData(_catalogService.AllocateResidentId(), displayName, identity);
        _scheduleService.EnsureDefaultAutonomySchedules(data);
        _catalogService.AddResident(data);
        _runtimeService.BindResident(data.Id, targetCharacter);
        data.PresenceSnapshot.SetWorldPosition(targetCharacter.transform.position, targetCharacter.transform.eulerAngles.y);
        _visualService.EnsureMarker(data);

        _log.LogInfo($"Registered NPC #{data.Id}: '{data.DisplayName}' with {(createdIdentity ? "new" : "existing")} identity seed={identity.GenerationSeed}, generatedRole={identity.Role}, female={identity.Appearance.IsFemale}.");
    }

    public void ForceAssignAtCrosshair()
    {
        if (TryGetTargetRegisteredResident(out var targetedResident))
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

        var crosshairDescription = DescribeCrosshairTarget();
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

        if (_craftStationService.TryGetCraftStationAtCrosshair(out var craftStationData))
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

        _log.LogWarning($"Cannot force assign resident #{pendingResident.Id}: target an innkeeper slot, a designated seat, a designated bed, a designated craft station, or another registered resident. Crosshair={crosshairDescription}.");
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
        _scheduleService.EnsureDefaultAutonomySchedules(resident);

        if (!resident.AssignedSeatId.HasValue)
        {
            return;
        }

        if (!_seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
        {
            return;
        }

        if (seatData.UsageType != SeatUsageType.Public)
        {
            return;
        }

        resident.ClearAssignedSeat();
        _scheduleService.ClearAssignedSeatSchedule(resident);

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
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var character = hitInfo.collider.GetComponentInParent<Character>();
                if (character != null)
                {
                    targetCharacter = character;
                    return true;
                }
            }
        }

        targetCharacter = null!;
        return false;
    }

    private bool TryGetTargetRegisteredResident(out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            resident = null!;
            return false;
        }

        if (!_runtimeService.TryGetResidentId(targetCharacter, out var residentId) || !_catalogService.TryGetResidentById(residentId, out resident))
        {
            resident = null!;
            return false;
        }

        return true;
    }

    private bool TryGetTargetRegisteredResident(string actionLabel, out Character targetCharacter, out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out targetCharacter))
        {
            _log.LogWarning($"{actionLabel}: no valid character is under the crosshair.");
            resident = null!;
            return false;
        }

        if (!_runtimeService.TryGetResidentId(targetCharacter, out var residentId) || !_catalogService.TryGetResidentById(residentId, out resident))
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
        var existingIdentity = targetCharacter.GetComponent<WyrdrasilVikingIdentityComponent>()?.Identity;
        if (existingIdentity != null)
        {
            createdIdentity = false;
            return existingIdentity;
        }

        var identity = _identityGenerator.Generate(defaultRole);
        try { _customizationApplier.Apply(targetCharacter.gameObject, identity); }
        catch (Exception) { }
        createdIdentity = true;
        return identity;
    }

}
