using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentRuntimeService
{
    private sealed class ResidentRuntimeBinding
    {
        public int ResidentId { get; }
        public int CharacterInstanceId { get; }
        public Character Character { get; }

        public ResidentRuntimeBinding(int residentId, Character character)
        {
            ResidentId = residentId;
            Character = character;
            CharacterInstanceId = character.gameObject.GetInstanceID();
        }
    }

    private readonly ManualLogSource _log;
    private readonly RegistryNpcNavigationService _navigationService;
    private readonly RegistryWaypointService _waypointService;
    private readonly Dictionary<int, ResidentRuntimeBinding> _bindingsByResidentId = new();
    private readonly Dictionary<int, int> _residentIdsByCharacterInstanceId = new();
    private readonly Dictionary<int, ResidentRuntimeState> _runtimeStatesByResidentId = new();

    public RegistryResidentRuntimeService(
        ManualLogSource log,
        RegistryNpcNavigationService navigationService,
        RegistryWaypointService waypointService)
    {
        _log = log;
        _navigationService = navigationService;
        _waypointService = waypointService;
    }

    public ResidentRuntimeState GetRuntimeState(int residentId)
    {
        var runtimeState = PeekRuntimeState(residentId);

        if (runtimeState == ResidentRuntimeState.Bound &&
            (!_bindingsByResidentId.TryGetValue(residentId, out var binding) || !binding.Character))
        {
            RemoveBinding(residentId, false);
            TransitionRuntimeState(residentId, ResidentRuntimeState.Missing);
            return ResidentRuntimeState.Missing;
        }

        return runtimeState;
    }

    public void MarkResidentSpawning(int residentId)
    {
        RemoveBinding(residentId, false);
        TransitionRuntimeState(residentId, ResidentRuntimeState.Spawning);
    }

    public void MarkResidentMissing(int residentId)
    {
        RemoveBinding(residentId, false);
        TransitionRuntimeState(residentId, ResidentRuntimeState.Missing);
    }

    public void BindResident(int residentId, Character character)
    {
        if (character == null)
        {
            _log.LogWarning($"Cannot bind resident #{residentId}: runtime character is not available.");
            TransitionRuntimeState(residentId, ResidentRuntimeState.Missing);
            return;
        }

        RemoveBinding(residentId, false);

        var characterInstanceId = character.gameObject.GetInstanceID();
        if (_residentIdsByCharacterInstanceId.TryGetValue(characterInstanceId, out var existingResidentId))
        {
            RemoveBinding(existingResidentId, true);
        }

        var binding = new ResidentRuntimeBinding(residentId, character);
        _bindingsByResidentId[residentId] = binding;
        _residentIdsByCharacterInstanceId[characterInstanceId] = residentId;
        TransitionRuntimeState(residentId, ResidentRuntimeState.Bound);
    }

    public void UnbindResident(int residentId)
    {
        RemoveBinding(residentId, true);
    }

    public bool TryDespawnResident(int residentId)
    {
        if (!TryGetBoundCharacter(residentId, out var character))
        {
            return false;
        }

        Object.Destroy(character.gameObject);
        RemoveBinding(residentId, false);
        TransitionRuntimeState(residentId, ResidentRuntimeState.Missing);
        _log.LogInfo($"Despawned runtime character for resident #{residentId}.");
        return true;
    }

    public bool TryGetResidentId(Character character, out int residentId)
    {
        if (character == null)
        {
            residentId = default;
            return false;
        }

        var characterInstanceId = character.gameObject.GetInstanceID();
        if (!_residentIdsByCharacterInstanceId.TryGetValue(characterInstanceId, out residentId))
        {
            residentId = default;
            return false;
        }

        if (_bindingsByResidentId.TryGetValue(residentId, out var binding) &&
            binding.Character &&
            binding.Character.gameObject.GetInstanceID() == characterInstanceId)
        {
            return true;
        }

        RemoveBinding(residentId, true);
        residentId = default;
        return false;
    }

    public bool TryGetBoundCharacter(int residentId, out Character character)
    {
        if (!_bindingsByResidentId.TryGetValue(residentId, out var binding))
        {
            character = null!;
            return false;
        }

        if (binding.Character)
        {
            character = binding.Character;
            return true;
        }

        RemoveBinding(residentId, true);
        character = null!;
        return false;
    }

    public void ApplyInnkeeperAssignment(RegisteredNpcData resident, ZoneSlotData slot)
    {
        ApplyPositionAssignment(resident.Id, slot.Position, $"innkeeper slot #{slot.Id}");
    }

    public void ApplySeatAssignment(RegisteredNpcData resident, RegisteredSeatData seat)
    {
        if (!TryGetBoundCharacter(resident.Id, out var character))
        {
            _log.LogWarning($"Cannot apply assignment to seat #{seat.Id}: resident runtime character is no longer available.");
            return;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, seat.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToSeat(character, routePoints, seat);
            _log.LogInfo($"Applied navigation graph route with {routePoints.Count} waypoint step(s) for seat #{seat.Id}.");
            return;
        }

        _navigationService.NavigateDirectlyToSeat(character, seat);
        _log.LogWarning($"No connected waypoint route was found for seat #{seat.Id}. Falling back to direct movement.");
    }

    private void ApplyPositionAssignment(int residentId, Vector3 targetPosition, string targetLabel)
    {
        if (!TryGetBoundCharacter(residentId, out var character))
        {
            _log.LogWarning($"Cannot apply assignment to {targetLabel}: resident runtime character is no longer available.");
            return;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, targetPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRoute(character, routePoints, targetPosition);
            _log.LogInfo($"Applied navigation graph route with {routePoints.Count} waypoint step(s) for {targetLabel}.");
            return;
        }

        _navigationService.NavigateDirectlyToAssignedSlot(character, targetPosition);
        _log.LogWarning($"No connected waypoint route was found for {targetLabel}. Falling back to direct movement.");
    }

    private void RemoveBinding(int residentId, bool markMissing)
    {
        if (_bindingsByResidentId.TryGetValue(residentId, out var binding))
        {
            _bindingsByResidentId.Remove(residentId);
            _residentIdsByCharacterInstanceId.Remove(binding.CharacterInstanceId);
        }

        if (markMissing)
        {
            TransitionRuntimeState(residentId, ResidentRuntimeState.Missing);
        }
    }

    private ResidentRuntimeState PeekRuntimeState(int residentId)
    {
        return _runtimeStatesByResidentId.TryGetValue(residentId, out var runtimeState)
            ? runtimeState
            : ResidentRuntimeState.Missing;
    }

    private void TransitionRuntimeState(int residentId, ResidentRuntimeState nextState)
    {
        var previousState = PeekRuntimeState(residentId);
        if (previousState == nextState)
        {
            return;
        }

        _runtimeStatesByResidentId[residentId] = nextState;
        _log.LogInfo($"Resident #{residentId} runtime state: {previousState} -> {nextState}.");
    }
}
