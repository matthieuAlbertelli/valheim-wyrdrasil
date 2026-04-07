using System;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySpawnService
{
    private readonly ManualLogSource _log;
    private readonly RegistryVikingPrefabFactory _vikingPrefabFactory;
    private readonly RegistryNpcIdentityGenerator _identityGenerator;
    private readonly RegistryNpcCustomizationApplier _customizationApplier;

    public RegistrySpawnService(
        ManualLogSource log,
        RegistryVikingPrefabFactory vikingPrefabFactory,
        RegistryNpcIdentityGenerator identityGenerator,
        RegistryNpcCustomizationApplier customizationApplier)
    {
        _log = log;
        _vikingPrefabFactory = vikingPrefabFactory;
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
    }

    public void SpawnTestViking()
    {
        var localPlayer = Player.m_localPlayer;

        if (!localPlayer)
        {
            _log.LogWarning("Cannot spawn test NPC: local player is not available.");
            return;
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

        if (!_vikingPrefabFactory.TryInstantiate(displayName, spawnPosition, spawnRotation, out var instance, out var vikingNpc) ||
            instance == null ||
            vikingNpc == null)
        {
            _log.LogWarning("Cannot spawn test NPC: registry viking prefab instantiation failed.");
            return;
        }

        try
        {
            _customizationApplier.Apply(instance, identity);
            instance.SetActive(true);
        }
        catch (Exception exception)
        {
            _log.LogWarning($"Cannot spawn test NPC: customization failed with {exception.GetType().Name}: {exception.Message}");
            UnityEngine.Object.Destroy(instance);
            return;
        }

        _log.LogInfo(
            $"Spawned player-derived registry viking at {spawnPosition}. seed={identity.GenerationSeed}, role={identity.Role}, female={identity.Appearance.IsFemale}, hair={identity.Appearance.HairItem}, beard={identity.Appearance.BeardItem ?? "<none>"}.");
    }
}