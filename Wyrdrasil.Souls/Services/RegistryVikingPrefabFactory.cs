using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;

namespace Wyrdrasil.Registry.Services;

/// <summary>
/// Builds an inactive runtime prefab from Valheim's Player prefab.
/// We keep the player rig/sync stack intact long enough to copy the shared Character/Humanoid state,
/// then strip player-specific components and replace them with the registry runtime humanoid.
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

    public bool TryInstantiate(
        string displayName,
        Vector3 position,
        Quaternion rotation,
        out GameObject? instance,
        out WyrdrasilVikingNpc? vikingNpc)
    {
        instance = null;
        vikingNpc = null;

        if (!TryBuildPrefab(out var prefab))
        {
            return false;
        }

        instance = Object.Instantiate(prefab, position, rotation);
        instance.name = displayName;
        instance.SetActive(false);

        vikingNpc = instance.GetComponent<WyrdrasilVikingNpc>();
        if (!vikingNpc)
        {
            _log.LogWarning("Cannot instantiate registry viking: runtime prefab is missing WyrdrasilVikingNpc.");
            Object.Destroy(instance);
            instance = null;
            return false;
        }

        vikingNpc.ApplyDisplayName(displayName);
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

        EnsureHiddenRoot();

        var runtimePrefab = Object.Instantiate(playerPrefab, _hiddenRoot!.transform, false);
        runtimePrefab.name = "Wyrdrasil_RuntimeVikingPrefab";
        runtimePrefab.SetActive(false);

        var runtimePlayer = runtimePrefab.GetComponent<Player>();
        if (!runtimePlayer)
        {
            _log.LogWarning("Cannot build registry viking prefab: instantiated runtime Player is missing.");
            Object.DestroyImmediate(runtimePrefab);
            prefab = null!;
            return false;
        }

        var runtimeVikingNpc = runtimePrefab.GetComponent<WyrdrasilVikingNpc>();
        if (!runtimeVikingNpc)
        {
            runtimeVikingNpc = runtimePrefab.AddComponent<WyrdrasilVikingNpc>();
        }

        runtimeVikingNpc.InitializeFromTemplate(runtimePlayer, "Wyrdrasil Test Viking");

        DestroyImmediateIfPresent<PlayerController>(runtimePrefab);
        DestroyImmediateIfPresent<Player>(runtimePrefab);
        DestroyImmediateIfPresent<Talker>(runtimePrefab);
        DestroyImmediateIfPresent<Skills>(runtimePrefab);

        var vikingAi = runtimePrefab.GetComponent<WyrdrasilVikingNpcAI>();
        if (!vikingAi)
        {
            vikingAi = runtimePrefab.AddComponent<WyrdrasilVikingNpcAI>();
        }

        var identityComponent = runtimePrefab.GetComponent<WyrdrasilVikingIdentityComponent>();
        if (!identityComponent)
        {
            runtimePrefab.AddComponent<WyrdrasilVikingIdentityComponent>();
        }

        var visualBootstrap = runtimePrefab.GetComponent<WyrdrasilVikingVisualBootstrap>();
        if (!visualBootstrap)
        {
            runtimePrefab.AddComponent<WyrdrasilVikingVisualBootstrap>();
        }

        ConfigureAi(vikingAi);

        if (runtimePrefab.TryGetComponent(out ZNetView zNetView))
        {
            zNetView.m_persistent = false;
        }

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