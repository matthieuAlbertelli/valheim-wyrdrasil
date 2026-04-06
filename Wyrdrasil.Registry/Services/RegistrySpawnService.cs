using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySpawnService
{
    private const string StableHumanoidPrefabName = "Dverger";

    private readonly ManualLogSource _log;

    public RegistrySpawnService(ManualLogSource log)
    {
        _log = log;
    }

    public void SpawnTestViking()
    {
        var localPlayer = Player.m_localPlayer;

        if (!localPlayer)
        {
            _log.LogWarning("Cannot spawn test NPC: local player is not available.");
            return;
        }

        var zNetScene = ZNetScene.instance;

        if (!zNetScene)
        {
            _log.LogWarning("Cannot spawn test NPC: ZNetScene is not ready.");
            return;
        }

        var prefab = zNetScene.GetPrefab(StableHumanoidPrefabName);

        if (!prefab)
        {
            _log.LogWarning($"Cannot spawn test NPC: base prefab '{StableHumanoidPrefabName}' was not found.");
            return;
        }

        var spawnPosition = localPlayer.transform.position + localPlayer.transform.forward * 3f;

        var zoneSystem = ZoneSystem.instance;
        if (zoneSystem != null)
        {
            spawnPosition.y = zoneSystem.GetGroundHeight(spawnPosition) + 0.5f;
        }

        var spawnRotation = Quaternion.LookRotation(-localPlayer.transform.forward);
        var instance = Object.Instantiate(prefab, spawnPosition, spawnRotation);
        instance.name = "WyrdrasilTestViking";

        EnsureMarker(instance, "Wyrdrasil Test Viking");
        TrySetCharacterName(instance, "Wyrdrasil Test Viking");

        _log.LogInfo($"Spawned test NPC from stable base prefab '{prefab.name}' at {spawnPosition}.");
    }

    private static void EnsureMarker(GameObject instance, string displayName)
    {
        var marker = instance.GetComponent<WyrdrasilTestNpcMarker>();

        if (!marker)
        {
            marker = instance.AddComponent<WyrdrasilTestNpcMarker>();
        }

        marker.DisplayName = displayName;
    }

    private void TrySetCharacterName(GameObject instance, string displayName)
    {
        var character = instance.GetComponent<Character>();

        if (!character)
        {
            return;
        }

        var nameField = typeof(Character).GetField(
            "m_name",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (nameField == null)
        {
            _log.LogWarning("Could not set test NPC name: Character.m_name was not found.");
            return;
        }

        nameField.SetValue(character, displayName);
    }
}
