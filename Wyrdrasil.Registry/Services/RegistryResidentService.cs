using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentService
{
    private readonly ManualLogSource _log;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryResidentRuntimeService _runtimeService;
    private readonly RegistrySpawnService _spawnService;
    private readonly RegistryNpcIdentityGenerator _identityGenerator;
    private readonly RegistryNpcCustomizationApplier _customizationApplier;
    private readonly List<RegisteredNpcData> _registeredNpcs = new();
    private readonly Dictionary<int, RegisteredNpcData> _registeredById = new();
    private readonly Dictionary<int, WyrdrasilRegisteredNpcMarker> _markers = new();

    private int _nextRegisteredNpcId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _registeredNpcs;

    public RegistryResidentService(
        ManualLogSource log,
        RegistryModeService modeService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryResidentRuntimeService runtimeService,
        RegistrySpawnService spawnService,
        RegistryNpcIdentityGenerator identityGenerator,
        RegistryNpcCustomizationApplier customizationApplier)
    {
        _log = log;
        _slotService = slotService;
        _seatService = seatService;
        _runtimeService = runtimeService;
        _spawnService = spawnService;
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
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

        var data = new RegisteredNpcData(
            _nextRegisteredNpcId++,
            displayName,
            identity);

        _registeredNpcs.Add(data);
        _registeredById[data.Id] = data;
        _runtimeService.BindResident(data.Id, targetCharacter);
        EnsureMarker(data);

        _log.LogInfo(
            $"Registered NPC #{data.Id}: '{data.DisplayName}' with {(createdIdentity ? "new" : "existing")} identity " +
            $"seed={identity.GenerationSeed}, generatedRole={identity.Role}, female={identity.Appearance.IsFemale}.");
    }

    public void RespawnAssignedResidentAtCrosshair()
    {
        if (_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            RespawnResidentAssignedToSlot(slotData);
            return;
        }

        if (_seatService.TryGetSeatAtCrosshair(out var seatData))
        {
            RespawnResidentAssignedToSeat(seatData);
            return;
        }

        _log.LogWarning("Cannot respawn resident: the targeted object is neither a registered innkeeper slot nor a registered designated seat.");
    }

    public void AssignInnkeeperRoleAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot assign innkeeper role", out var targetCharacter, out var data))
        {
            return;
        }

        DetachIfAttached(targetCharacter);
        _slotService.ClearAssignmentForResident(data.Id);
        _seatService.ClearAssignmentForResident(data.Id);
        data.ClearAssignedSeat();

        if (!_slotService.TryAssignInnkeeperSlot(data.Id, out var slotData) || slotData == null)
        {
            _log.LogWarning("Cannot assign innkeeper role: no free Innkeeper slot is available.");
            return;
        }

        data.SetRole(NpcRole.Innkeeper);
        data.AssignSlot(slotData.Id);
        UpdateMarker(data);
        _runtimeService.ApplyInnkeeperAssignment(data, slotData);
        _log.LogInfo($"Assigned Innkeeper role to registered NPC #{data.Id} ('{data.DisplayName}') using slot #{slotData.Id}.");
    }

    public void AssignSeatAtCrosshair()
    {
        if (!TryGetTargetRegisteredResident("Cannot assign seat", out var targetCharacter, out var data))
        {
            return;
        }

        if (data.Role == NpcRole.Innkeeper)
        {
            _log.LogWarning("Cannot assign seat: the targeted resident is already an Innkeeper.");
            return;
        }

        DetachIfAttached(targetCharacter);
        _seatService.ClearAssignmentForResident(data.Id);
        data.ClearAssignedSeat();

        if (!_seatService.TryAssignSeat(data.Id, out var seatData) || seatData == null)
        {
            _log.LogWarning("Cannot assign seat: no free designated seat is available.");
            return;
        }

        data.SetRole(NpcRole.Villager);
        data.AssignSeat(seatData.Id);
        UpdateMarker(data);
        _runtimeService.ApplySeatAssignment(data, seatData);
        _log.LogInfo($"Assigned designated seat #{seatData.Id} to registered NPC #{data.Id} ('{data.DisplayName}').");
    }

    public void HandleDeletedSlot(int slotId)
    {
        foreach (var resident in _registeredNpcs)
        {
            if (resident.AssignedSlotId != slotId)
            {
                continue;
            }

            resident.ClearAssignedSlot();
            resident.SetRole(NpcRole.Villager);
            UpdateMarker(resident);
            _log.LogInfo($"Cleared resident assignment for NPC #{resident.Id} because slot #{slotId} was deleted.");
        }
    }

    public void HandleDeletedSeat(int seatId)
    {
        foreach (var resident in _registeredNpcs)
        {
            if (resident.AssignedSeatId != seatId)
            {
                continue;
            }

            if (_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
            {
                DetachIfAttached(character);
            }

            resident.ClearAssignedSeat();
            UpdateMarker(resident);
            _log.LogInfo($"Cleared resident seat assignment for NPC #{resident.Id} because seat #{seatId} was deleted.");
        }
    }

    private void RespawnResidentAssignedToSlot(ZoneSlotData slotData)
    {
        if (!slotData.AssignedRegisteredNpcId.HasValue)
        {
            _log.LogWarning($"Cannot respawn resident: innkeeper slot #{slotData.Id} has no assigned resident.");
            return;
        }

        if (!_registeredById.TryGetValue(slotData.AssignedRegisteredNpcId.Value, out var resident))
        {
            _log.LogWarning($"Cannot respawn resident: slot #{slotData.Id} references unknown resident #{slotData.AssignedRegisteredNpcId.Value}.");
            return;
        }

        if (_runtimeService.TryGetBoundCharacter(resident.Id, out _))
        {
            _log.LogWarning($"Cannot respawn resident #{resident.Id}: a runtime character is already bound.");
            return;
        }

        var runtimeState = _runtimeService.GetRuntimeState(resident.Id);
        if (runtimeState == ResidentRuntimeState.Spawning)
        {
            _log.LogWarning($"Cannot respawn resident #{resident.Id}: the resident is already in a spawning transition.");
            return;
        }

        var spawnPosition = slotData.Position;
        var spawnRotation = BuildFacingRotation(spawnPosition, GetPlayerLookTargetOrFallback(spawnPosition));

        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _))
        {
            _log.LogWarning($"Cannot respawn resident #{resident.Id}: runtime spawn failed from slot #{slotData.Id}.");
            return;
        }

        _runtimeService.ApplyInnkeeperAssignment(resident, slotData);
        _log.LogInfo($"Respawned resident #{resident.Id} ('{resident.DisplayName}') from innkeeper slot #{slotData.Id}.");
    }

    private void RespawnResidentAssignedToSeat(RegisteredSeatData seatData)
    {
        if (!seatData.AssignedRegisteredNpcId.HasValue)
        {
            _log.LogWarning($"Cannot respawn resident: seat #{seatData.Id} has no assigned resident.");
            return;
        }

        if (!_registeredById.TryGetValue(seatData.AssignedRegisteredNpcId.Value, out var resident))
        {
            _log.LogWarning($"Cannot respawn resident: seat #{seatData.Id} references unknown resident #{seatData.AssignedRegisteredNpcId.Value}.");
            return;
        }

        if (_runtimeService.TryGetBoundCharacter(resident.Id, out _))
        {
            _log.LogWarning($"Cannot respawn resident #{resident.Id}: a runtime character is already bound.");
            return;
        }

        var runtimeState = _runtimeService.GetRuntimeState(resident.Id);
        if (runtimeState == ResidentRuntimeState.Spawning)
        {
            _log.LogWarning($"Cannot respawn resident #{resident.Id}: the resident is already in a spawning transition.");
            return;
        }

        var spawnPosition = seatData.ApproachPosition;
        var spawnRotation = BuildFacingRotation(spawnPosition, seatData.SeatPosition);

        if (!TrySpawnAndBindResident(resident, spawnPosition, spawnRotation, out _))
        {
            _log.LogWarning($"Cannot respawn resident #{resident.Id}: runtime spawn failed from seat #{seatData.Id}.");
            return;
        }

        _runtimeService.ApplySeatAssignment(resident, seatData);
        _log.LogInfo($"Respawned resident #{resident.Id} ('{resident.DisplayName}') from seat #{seatData.Id}.");
    }

    private bool TrySpawnAndBindResident(
        RegisteredNpcData resident,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out Character runtimeCharacter)
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
            _log.LogWarning($"Cannot bind respawned resident #{resident.Id}: spawned runtime object has no Character component.");
            UnityEngine.Object.Destroy(instance);
            _runtimeService.MarkResidentMissing(resident.Id);
            return false;
        }

        _runtimeService.BindResident(resident.Id, character);
        EnsureMarker(resident);
        runtimeCharacter = character;
        return true;
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values)
        {
            marker.SetVisualizationVisible(isEnabled);
        }
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

    private bool TryGetTargetRegisteredResident(string actionLabel, out Character targetCharacter, out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out targetCharacter))
        {
            _log.LogWarning($"{actionLabel}: no valid character is under the crosshair.");
            resident = null!;
            return false;
        }

        if (!_runtimeService.TryGetResidentId(targetCharacter, out var residentId) ||
            !_registeredById.TryGetValue(residentId, out resident))
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
        if (nameField?.GetValue(character) is string name && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        return character.gameObject.name;
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

        try
        {
            _customizationApplier.Apply(targetCharacter.gameObject, identity);
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Generated identity for '{targetCharacter.gameObject.name}', but runtime application failed with " +
                $"{exception.GetType().Name}: {exception.Message}");
        }

        createdIdentity = true;
        return identity;
    }

    private void EnsureMarker(RegisteredNpcData data)
    {
        if (!_runtimeService.TryGetBoundCharacter(data.Id, out var character))
        {
            _log.LogWarning($"Cannot create resident marker for NPC #{data.Id}: runtime character is not available.");
            return;
        }

        var marker = character.GetComponent<WyrdrasilRegisteredNpcMarker>();
        if (!marker)
        {
            marker = character.gameObject.AddComponent<WyrdrasilRegisteredNpcMarker>();
        }

        marker.Initialize(data.Id, data.DisplayName, data.Role);
        marker.EnsureVisual();
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[data.Id] = marker;
    }

    private void UpdateMarker(RegisteredNpcData data)
    {
        if (_markers.TryGetValue(data.Id, out var marker))
        {
            marker.UpdateRole(data.Role);
        }
    }

    private static void DetachIfAttached(Character character)
    {
        if (character is Humanoid humanoid && humanoid.IsAttached())
        {
            humanoid.AttachStop();
        }
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
        if (!localPlayer)
        {
            return fallbackPosition + Vector3.forward;
        }

        return localPlayer.transform.position;
    }
}
