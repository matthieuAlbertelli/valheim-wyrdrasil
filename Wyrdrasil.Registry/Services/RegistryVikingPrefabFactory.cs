using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;

namespace Wyrdrasil.Registry.Services;

/// <summary>
/// Builds an inactive runtime prefab from Valheim's Player prefab.
/// We keep the player rig/sync stack intact and remove only player-specific components,
/// following the same construction principle used by VikingNPC.
/// </summary>
public sealed class RegistryVikingPrefabFactory
{
    private const string PlayerPrefabName = "Player";
    private const string HiddenRootName = "Wyrdrasil.Registry.HiddenRuntimePrefabs";

    private readonly ManualLogSource _log;

    private GameObject? _cachedVikingPrefab;
    private GameObject? _hiddenRoot;

    public RegistryVikingPrefabFactory(ManualLogSource log)
    {
        _log = log;
    }

    public bool TrySpawn(string displayName, Vector3 position, Quaternion rotation, out Character? spawnedCharacter)
    {
        spawnedCharacter = null;

        if (!TryBuildPrefab(out var prefab))
        {
            return false;
        }

        var instance = Object.Instantiate(prefab, position, rotation);
        instance.name = displayName;
        instance.SetActive(true);

        var vikingNpc = instance.GetComponent<WyrdrasilVikingNpc>();
        if (!vikingNpc)
        {
            _log.LogWarning("Cannot spawn registry viking: runtime prefab is missing WyrdrasilVikingNpc.");
            Object.Destroy(instance);
            return false;
        }

        vikingNpc.ApplyDisplayName(displayName);
        spawnedCharacter = vikingNpc;
        return true;
    }

    private bool TryBuildPrefab(out GameObject prefab)
    {
        if (_cachedVikingPrefab != null)
        {
            prefab = _cachedVikingPrefab;
            return true;
        }

        var zNetScene = ZNetScene.instance;
        if (!zNetScene)
        {
            _log.LogWarning("Cannot build registry viking prefab: ZNetScene is not ready.");
            prefab = null!;
            return false;
        }

        var playerPrefab = zNetScene.GetPrefab(PlayerPrefabName);
        if (!playerPrefab)
        {
            _log.LogWarning($"Cannot build registry viking prefab: base prefab '{PlayerPrefabName}' was not found.");
            prefab = null!;
            return false;
        }

        var playerTemplate = playerPrefab.GetComponent<Player>();
        if (!playerTemplate)
        {
            _log.LogWarning("Cannot build registry viking prefab: Player component is missing on the player prefab.");
            prefab = null!;
            return false;
        }

        EnsureHiddenRoot();

        var runtimePrefab = Object.Instantiate(playerPrefab, _hiddenRoot!.transform, false);
        runtimePrefab.name = "Wyrdrasil_RuntimeVikingPrefab";
        runtimePrefab.SetActive(false);

        DestroyImmediateIfPresent<PlayerController>(runtimePrefab);
        DestroyImmediateIfPresent<Player>(runtimePrefab);
        DestroyImmediateIfPresent<Talker>(runtimePrefab);
        DestroyImmediateIfPresent<Skills>(runtimePrefab);

        var vikingNpc = runtimePrefab.GetComponent<WyrdrasilVikingNpc>();
        if (!vikingNpc)
        {
            vikingNpc = runtimePrefab.AddComponent<WyrdrasilVikingNpc>();
        }

        vikingNpc.InitializeFromTemplate(playerTemplate, "Wyrdrasil Test Viking");

        var vikingAi = runtimePrefab.GetComponent<WyrdrasilVikingNpcAI>();
        if (!vikingAi)
        {
            vikingAi = runtimePrefab.AddComponent<WyrdrasilVikingNpcAI>();
        }

        ConfigureAi(vikingAi);

        if (runtimePrefab.TryGetComponent(out ZNetView zNetView))
        {
            zNetView.m_persistent = false;
        }

        Object.DontDestroyOnLoad(runtimePrefab);
        _cachedVikingPrefab = runtimePrefab;
        prefab = runtimePrefab;

        _log.LogInfo("Built inactive player-derived registry viking runtime prefab.");
        return true;
    }

    private static void ConfigureAi(WyrdrasilVikingNpcAI ai)
    {
        ai.m_viewRange = 0f;
        ai.m_viewAngle = 90f;
        ai.m_hearRange = 0f;
        ai.m_idleSoundInterval = 999999f;
        ai.m_idleSoundChance = 0f;
        ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
        ai.m_moveMinAngle = 5f;
        ai.m_smoothMovement = false;
        ai.m_jumpInterval = 2f;
        ai.m_randomCircleInterval = 999999f;
        ai.m_randomMoveInterval = 999999f;
        ai.m_randomMoveRange = 0f;
        ai.m_alertRange = 0f;
        ai.m_circulateWhileCharging = false;
        ai.m_interceptTimeMax = 0f;
        ai.m_maxChaseDistance = 0f;
        ai.m_circleTargetInterval = 0f;
        ai.m_circleTargetDuration = 0f;
        ai.m_circleTargetDistance = 0f;
        ai.m_aggravatable = false;
        ai.m_attackPlayerObjects = false;
    }

    private void EnsureHiddenRoot()
    {
        if (_hiddenRoot != null)
        {
            return;
        }

        _hiddenRoot = new GameObject(HiddenRootName)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        _hiddenRoot.SetActive(false);
        Object.DontDestroyOnLoad(_hiddenRoot);
    }

    private static void DestroyImmediateIfPresent<T>(GameObject gameObject) where T : Component
    {
        var component = gameObject.GetComponent<T>();
        if (component != null)
        {
            Object.DestroyImmediate(component);
        }
    }
}
