using System;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentRegistrationService
{
    private readonly ManualLogSource _log;
    private readonly RegistryResidentTargetingService _targetingService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly NpcIdentityGenerator _identityGenerator;
    private readonly NpcCustomizationApplier _customizationApplier;
    private readonly ResidentCatalogService _catalogService;
    private readonly ResidentVisualService _visualService;
    private readonly ResidentScheduleService _scheduleService;

    public RegistryResidentRegistrationService(
        ManualLogSource log,
        RegistryResidentTargetingService targetingService,
        ResidentRuntimeService runtimeService,
        NpcIdentityGenerator identityGenerator,
        NpcCustomizationApplier customizationApplier,
        ResidentCatalogService catalogService,
        ResidentVisualService visualService,
        ResidentScheduleService scheduleService)
    {
        _log = log;
        _targetingService = targetingService;
        _runtimeService = runtimeService;
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
        _catalogService = catalogService;
        _visualService = visualService;
        _scheduleService = scheduleService;
    }

    public void RegisterNpcAtCrosshair()
    {
        if (!_targetingService.TryGetTargetCharacter(out var targetCharacter))
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

    public void NormalizeResidentAfterLoad(RegisteredNpcData resident, SeatService seatService)
    {
        _scheduleService.EnsureDefaultAutonomySchedules(resident);

        if (!resident.AssignedSeatId.HasValue)
        {
            return;
        }

        if (!seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
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

    private static string GetCharacterName(Character character)
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
        try
        {
            _customizationApplier.Apply(targetCharacter.gameObject, identity);
        }
        catch (Exception)
        {
        }

        createdIdentity = true;
        return identity;
    }
}
