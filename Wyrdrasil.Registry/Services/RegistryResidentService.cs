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
    private readonly List<RegisteredNpcData> _registeredNpcs = new();
    private readonly Dictionary<int, RegisteredNpcData> _registeredByCharacterInstanceId = new();
    private readonly Dictionary<int, WyrdrasilRegisteredNpcMarker> _markers = new();

    private int _nextRegisteredNpcId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<RegisteredNpcData> RegisteredNpcs => _registeredNpcs;

    public RegistryResidentService(
        ManualLogSource log,
        RegistryModeService modeService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryResidentRuntimeService runtimeService)
    {
        _log = log;
        _slotService = slotService;
        _seatService = seatService;
        _runtimeService = runtimeService;
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

        var characterInstanceId = targetCharacter.gameObject.GetInstanceID();
        if (_registeredByCharacterInstanceId.ContainsKey(characterInstanceId))
        {
            _log.LogWarning("Cannot register NPC: this character is already registered.");
            return;
        }

        var displayName = GetCharacterName(targetCharacter);
        var data = new RegisteredNpcData(_nextRegisteredNpcId++, characterInstanceId, displayName, targetCharacter);
        _registeredNpcs.Add(data);
        _registeredByCharacterInstanceId[characterInstanceId] = data;
        EnsureMarker(data);
        _log.LogInfo($"Registered NPC #{data.Id}: '{data.DisplayName}'.");
    }

    public void AssignInnkeeperRoleAtCrosshair()
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            _log.LogWarning("Cannot assign innkeeper role: no valid character is under the crosshair.");
            return;
        }

        var characterInstanceId = targetCharacter.gameObject.GetInstanceID();
        if (!_registeredByCharacterInstanceId.TryGetValue(characterInstanceId, out var data))
        {
            _log.LogWarning("Cannot assign innkeeper role: the targeted character is not registered.");
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
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            _log.LogWarning("Cannot assign seat: no valid character is under the crosshair.");
            return;
        }

        var characterInstanceId = targetCharacter.gameObject.GetInstanceID();
        if (!_registeredByCharacterInstanceId.TryGetValue(characterInstanceId, out var data))
        {
            _log.LogWarning("Cannot assign seat: the targeted character is not registered.");
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

            DetachIfAttached(resident.Character);
            resident.ClearAssignedSeat();
            UpdateMarker(resident);
            _log.LogInfo($"Cleared resident seat assignment for NPC #{resident.Id} because seat #{seatId} was deleted.");
        }
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

    private string GetCharacterName(Character character)
    {
        var nameField = typeof(Character).GetField("m_name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (nameField?.GetValue(character) is string name && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        return character.gameObject.name;
    }

    private void EnsureMarker(RegisteredNpcData data)
    {
        var marker = data.Character.GetComponent<WyrdrasilRegisteredNpcMarker>();
        if (!marker)
        {
            marker = data.Character.gameObject.AddComponent<WyrdrasilRegisteredNpcMarker>();
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
}
