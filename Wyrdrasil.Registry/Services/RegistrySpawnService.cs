using BepInEx.Logging;
using UnityEngine;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySpawnService
{
    private readonly ManualLogSource _log;
    private readonly RegistryVikingPrefabFactory _vikingPrefabFactory;

    public RegistrySpawnService(ManualLogSource log, RegistryVikingPrefabFactory vikingPrefabFactory)
    {
        _log = log;
        _vikingPrefabFactory = vikingPrefabFactory;
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
        if (!_vikingPrefabFactory.TrySpawn("Wyrdrasil Test Viking", spawnPosition, spawnRotation, out var spawnedCharacter) || spawnedCharacter == null)
        {
            _log.LogWarning("Cannot spawn test NPC: registry viking prefab instantiation failed.");
            return;
        }

        _log.LogInfo($"Spawned player-derived registry viking at {spawnPosition}.");
    }
}
