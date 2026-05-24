using System;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Services;


public sealed class NpcSpawnService
{
    private readonly ManualLogSource _log;
    private readonly VikingPrefabFactory _vikingPrefabFactory;
    private readonly NpcIdentityGenerator _identityGenerator;
    private readonly NpcCustomizationApplier _customizationApplier;

    public NpcSpawnService(
        ManualLogSource log,
        VikingPrefabFactory vikingPrefabFactory,
        NpcIdentityGenerator identityGenerator,
        NpcCustomizationApplier customizationApplier)
    {
        _log = log;
        _vikingPrefabFactory = vikingPrefabFactory;
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
    }

    public void SpawnTestViking()
    {
        if (!TrySpawnTestViking(out _, out var failureReason))
        {
            _log.LogWarning(failureReason);
        }
    }

    public bool TrySpawnTestViking(out Character? spawnedCharacter, out string failureReason)
    {
        spawnedCharacter = null;
        failureReason = string.Empty;

        var localPlayer = Player.m_localPlayer;

        if (!localPlayer)
        {
            failureReason = "Cannot spawn test NPC: local player is not available.";
            return false;
        }

        var spawnPosition = localPlayer.transform.position + localPlayer.transform.forward * 3f;

        var zoneSystem = ZoneSystem.instance;
        if (zoneSystem != null)
        {
            spawnPosition.y = zoneSystem.GetGroundHeight(spawnPosition) + 0.5f;
        }

        var spawnRotation = Quaternion.LookRotation(-localPlayer.transform.forward);
        var identity = _identityGenerator.Generate(NpcRole.Villager);
        var displayName = $"Wyrdrasil Test Viking #{identity.GenerationSeed & 0xFF:X2}";

        if (!TrySpawnViking(displayName, spawnPosition, spawnRotation, identity, out var instance) || instance == null)
        {
            failureReason = "Cannot spawn test NPC: runtime viking instantiation failed.";
            return false;
        }

        spawnedCharacter = instance.GetComponent<Character>();
        if (spawnedCharacter == null)
        {
            UnityEngine.Object.Destroy(instance);
            failureReason = "Cannot spawn test NPC: spawned runtime viking has no Character component.";
            return false;
        }

        _log.LogInfo(
            $"Spawned player-derived registry viking at {spawnPosition}. seed={identity.GenerationSeed}, role={identity.Role}, female={identity.Appearance.IsFemale}, hair={identity.Appearance.HairItem}, beard={identity.Appearance.BeardItem ?? "<none>"}.");

        return true;
    }

    public bool TrySpawnResident(
        RegisteredNpcData resident,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        out GameObject? instance)
    {
        if (resident == null)
        {
            throw new ArgumentNullException(nameof(resident));
        }

        return TrySpawnViking(resident.DisplayName, spawnPosition, spawnRotation, resident.Identity, out instance);
    }

    public bool TrySpawnViking(
        string displayName,
        Vector3 spawnPosition,
        Quaternion spawnRotation,
        VikingIdentityData identity,
        out GameObject? instance)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Spawned NPC display name cannot be null or whitespace.", nameof(displayName));
        }

        if (identity == null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        if (!_vikingPrefabFactory.TryInstantiate(displayName, spawnPosition, spawnRotation, out instance, out var vikingNpc) ||
            instance == null ||
            vikingNpc == null)
        {
            _log.LogWarning("Cannot spawn registry viking: registry viking prefab instantiation failed.");
            instance = null;
            return false;
        }

        try
        {
            _customizationApplier.Apply(instance, identity);
            instance.SetActive(true);
            return true;
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Cannot spawn registry viking '{displayName}': customization failed with {exception.GetType().Name}: {exception.Message}");

            UnityEngine.Object.Destroy(instance);
            instance = null;
            return false;
        }
    }
}
