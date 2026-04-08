using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentService
{
    private readonly ManualLogSource _log;
    private readonly RegistryToolState _toolState;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryBedService _bedService;
    private readonly RegistryResidentRuntimeService _runtimeService;
    private readonly RegistryNpcIdentityGenerator _identityGenerator;
    private readonly RegistryNpcCustomizationApplier _customizationApplier;
    private readonly RegistryResidentCatalogService _catalogService;
    private readonly RegistryResidentVisualService _visualService;
    private readonly RegistryResidentPresenceService _presenceService;
    private readonly RegistryResidentAssignmentService _assignmentService;

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _catalogService.RegisteredNpcs;
    public int NextRegisteredNpcId => _catalogService.NextRegisteredNpcId;

    public RegistryResidentService(
        ManualLogSource log,
        RegistryToolState toolState,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryBedService bedService,
        RegistryResidentRuntimeService runtimeService,
        RegistryNpcIdentityGenerator identityGenerator,
        RegistryNpcCustomizationApplier customizationApplier,
        RegistryResidentCatalogService catalogService,
        RegistryResidentVisualService visualService,
        RegistryResidentPresenceService presenceService,
        RegistryResidentAssignmentService assignmentService)
    {
        _log = log;
        _toolState = toolState;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _runtimeService = runtimeService;
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
        _catalogService = catalogService;
        _visualService = visualService;
        _presenceService = presenceService;
        _assignmentService = assignmentService;
    }

    public IReadOnlyDictionary<int, WyrdrasilRegisteredNpcMarker> Markers => _visualService.Markers;

    public void LoadResidents(IEnumerable<RegisteredNpcData> residents, int nextResidentId)
    {
        _catalogService.LoadResidents(residents, nextResidentId);
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

        if (_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            if (_assignmentService.TryForceAssignToSlot(pendingResident, slotData))
            {
                _toolState.ClearPendingResidentForceAssign();
            }

            return;
        }

        if (_seatService.TryGetSeatAtCrosshair(out var seatData))
        {
            if (_assignmentService.TryForceAssignToSeat(pendingResident, seatData))
            {
                _toolState.ClearPendingResidentForceAssign();
            }

            return;
        }

        if (_bedService.TryGetBedAtCrosshair(out var bedData))
        {
            if (_assignmentService.TryForceAssignToBed(pendingResident, bedData))
            {
                _toolState.ClearPendingResidentForceAssign();
            }

            return;
        }

        _log.LogWarning($"Cannot force assign resident #{pendingResident.Id}: target an innkeeper slot, a designated seat, a designated bed, or another registered resident.");
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

        _log.LogWarning("Cannot respawn resident: the targeted object is neither a registered innkeeper slot, designated seat, nor designated bed.");
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
        if (!TryGetTargetRegisteredResident("Cannot assign seat", out var targetCharacter, out var data)) return;
        if (data.Role == NpcRole.Innkeeper)
        {
            _log.LogWarning("Cannot assign seat: the targeted resident is already an Innkeeper.");
            return;
        }

        if (!_assignmentService.TryAssignSeat(data, targetCharacter, out var seatData) || seatData == null)
        {
            _log.LogWarning("Cannot assign seat: no free designated seat is available.");
            return;
        }
        _log.LogInfo($"Assigned designated seat #{seatData.Id} to registered NPC #{data.Id} ('{data.DisplayName}').");
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
