using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolItemService
{
    private readonly ManualLogSource _log;
    private readonly RegistryPlayerToolPieceTableService _pieceTableService;

    private GameObject? _hiddenRoot;
    private GameObject? _runtimeItemPrefab;
    private ObjectDB? _registeredObjectDb;
    private ZNetScene? _registeredZNetScene;
    private bool _registeredInNamedPrefabs;

    public RegistryPlayerToolItemService(
        ManualLogSource log,
        RegistryPlayerToolPieceTableService pieceTableService)
    {
        _log = log;
        _pieceTableService = pieceTableService;
    }

    public void Update()
    {
        var objectDb = ObjectDB.instance;
        var zNetScene = ZNetScene.instance;

        if (objectDb == null || zNetScene == null)
        {
            return;
        }

        if (!TryGetOrCreateRuntimeItemPrefab(zNetScene, out var runtimeItemPrefab))
        {
            return;
        }

        var wasRegisteredInScene = EnsurePrefabRegisteredInZNetScene(zNetScene, runtimeItemPrefab);
        var wasRegisteredInObjectDb = EnsureItemRegisteredInObjectDb(objectDb, runtimeItemPrefab);

        if (_registeredZNetScene != zNetScene || wasRegisteredInScene || !_registeredInNamedPrefabs)
        {
            _registeredInNamedPrefabs = RegisterNamedPrefabIfPossible(zNetScene, runtimeItemPrefab);
            _registeredZNetScene = zNetScene;
        }

        if (_registeredObjectDb != objectDb || wasRegisteredInObjectDb)
        {
            RegisterObjectDbItemHashIfPossible(objectDb, runtimeItemPrefab);
            RefreshObjectDbItemHashes(objectDb);
            _registeredObjectDb = objectDb;
        }
    }

    private bool TryGetOrCreateRuntimeItemPrefab(
        ZNetScene zNetScene,
        out GameObject runtimeItemPrefab)
    {
        if (_runtimeItemPrefab != null)
        {
            runtimeItemPrefab = _runtimeItemPrefab;
            runtimeItemPrefab.SetActive(true);

            var sourcePrefabForRefresh = zNetScene.GetPrefab(RegistryPlayerToolConstants.SourceItemPrefabName) ??
                                         FindPrefabByName(zNetScene, RegistryPlayerToolConstants.SourceItemPrefabName);
            var sourcePieceTable = sourcePrefabForRefresh
                ?.GetComponent<ItemDrop>()
                ?.m_itemData
                .m_shared
                .m_buildPieces;

            var refreshedPieceTable = _pieceTableService.GetOrCreate(zNetScene, sourcePieceTable);
            var existingItemDrop = runtimeItemPrefab.GetComponent<ItemDrop>();
            var activePieceTable = refreshedPieceTable ?? existingItemDrop?.m_itemData.m_shared.m_buildPieces;
            if (activePieceTable != null)
            {
                EnsureItemDataIsConfigured(runtimeItemPrefab, activePieceTable);
                TryRefreshEquippedRegistryToolBuildPieces(activePieceTable);
            }

            return true;
        }

        var sourcePrefab = zNetScene.GetPrefab(RegistryPlayerToolConstants.SourceItemPrefabName);
        if (sourcePrefab == null)
        {
            sourcePrefab = FindPrefabByName(zNetScene, RegistryPlayerToolConstants.SourceItemPrefabName);
        }

        if (sourcePrefab == null)
        {
            _log.LogWarning(
                $"Cannot create '{RegistryPlayerToolConstants.ItemPrefabName}': source item prefab " +
                $"'{RegistryPlayerToolConstants.SourceItemPrefabName}' was not found.");
            runtimeItemPrefab = null!;
            return false;
        }

        var sourceItemDrop = sourcePrefab.GetComponent<ItemDrop>();
        if (sourceItemDrop == null)
        {
            _log.LogWarning(
                $"Cannot create '{RegistryPlayerToolConstants.ItemPrefabName}': source item prefab " +
                $"'{RegistryPlayerToolConstants.SourceItemPrefabName}' has no ItemDrop component.");
            runtimeItemPrefab = null!;
            return false;
        }

        var pieceTable = _pieceTableService.GetOrCreate(
            zNetScene,
            sourceItemDrop.m_itemData.m_shared.m_buildPieces);
        if (pieceTable == null)
        {
            runtimeItemPrefab = null!;
            return false;
        }

        EnsureHiddenRoot();

        var instance = Object.Instantiate(sourcePrefab, _hiddenRoot!.transform, false);
        instance.name = RegistryPlayerToolConstants.ItemPrefabName;

        // Keep the prefab object's own active state enabled.
        // The hidden parent keeps the template out of the visible scene, but Valheim's spawn flow
        // clones activeSelf from the prefab object. If this template is inactive, the spawned item
        // can exist but remain invisible/inactive in the world.
        instance.SetActive(true);

        if (!EnsureItemDataIsConfigured(instance, pieceTable))
        {
            Object.Destroy(instance);
            runtimeItemPrefab = null!;
            return false;
        }

        _runtimeItemPrefab = instance;
        runtimeItemPrefab = instance;

        _log.LogInfo(
            $"Created player tool item prefab '{RegistryPlayerToolConstants.ItemPrefabName}' " +
            $"from vanilla '{RegistryPlayerToolConstants.SourceItemPrefabName}'.");

        return true;
    }


    private void TryRefreshEquippedRegistryToolBuildPieces(PieceTable pieceTable)
    {
        var player = Player.m_localPlayer;
        if (player == null || !RegistryPlayerToolSelectionService.IsRegistryToolActive(player))
        {
            return;
        }

        for (var type = player.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                "m_buildPieces",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null || field.FieldType != typeof(PieceTable))
            {
                continue;
            }

            field.SetValue(player, pieceTable);
            return;
        }
    }


    private void EnsureHiddenRoot()
    {
        if (_hiddenRoot != null)
        {
            return;
        }

        _hiddenRoot = new GameObject(RegistryPlayerToolConstants.HiddenRootName);
        _hiddenRoot.SetActive(false);
        Object.DontDestroyOnLoad(_hiddenRoot);
    }

    private bool EnsureItemDataIsConfigured(GameObject itemPrefab, PieceTable pieceTable)
    {
        var itemDrop = itemPrefab.GetComponent<ItemDrop>();
        if (itemDrop == null)
        {
            _log.LogWarning(
                $"Cannot configure '{RegistryPlayerToolConstants.ItemPrefabName}': prefab has no ItemDrop component.");
            return false;
        }

        itemDrop.m_itemData.m_dropPrefab = itemPrefab;

        var sharedData = itemDrop.m_itemData.m_shared;
        sharedData.m_name = RegistryPlayerToolConstants.DisplayName;
        sharedData.m_description = RegistryPlayerToolConstants.Description;
        sharedData.m_buildPieces = pieceTable;

        EnsureDropPrefabBinder(itemPrefab, itemDrop);

        return true;
    }

    private static void EnsureDropPrefabBinder(GameObject itemPrefab, ItemDrop itemDrop)
    {
        var binder = itemPrefab.GetComponent<RegistryPlayerToolItemDropPrefabBinder>();
        if (binder == null)
        {
            binder = itemPrefab.AddComponent<RegistryPlayerToolItemDropPrefabBinder>();
        }

        binder.RuntimePrefab = itemPrefab;
        binder.BindDropPrefab();
        itemDrop.m_itemData.m_dropPrefab = itemPrefab;
    }

    private bool EnsurePrefabRegisteredInZNetScene(ZNetScene zNetScene, GameObject itemPrefab)
    {
        var existingIndex = FindPrefabIndex(zNetScene.m_prefabs, RegistryPlayerToolConstants.ItemPrefabName);
        if (existingIndex >= 0)
        {
            if (zNetScene.m_prefabs[existingIndex] == itemPrefab)
            {
                return false;
            }

            zNetScene.m_prefabs[existingIndex] = itemPrefab;
            _log.LogInfo($"Replaced stale '{RegistryPlayerToolConstants.ItemPrefabName}' prefab in ZNetScene.");
            return true;
        }

        zNetScene.m_prefabs.Add(itemPrefab);
        _log.LogInfo($"Registered '{RegistryPlayerToolConstants.ItemPrefabName}' in ZNetScene.");
        return true;
    }

    private bool EnsureItemRegisteredInObjectDb(ObjectDB objectDb, GameObject itemPrefab)
    {
        var existingIndex = FindPrefabIndex(objectDb.m_items, RegistryPlayerToolConstants.ItemPrefabName);
        if (existingIndex >= 0)
        {
            if (objectDb.m_items[existingIndex] == itemPrefab)
            {
                return false;
            }

            objectDb.m_items[existingIndex] = itemPrefab;
            _log.LogInfo($"Replaced stale '{RegistryPlayerToolConstants.ItemPrefabName}' item in ObjectDB.");
            return true;
        }

        objectDb.m_items.Add(itemPrefab);
        _log.LogInfo($"Registered '{RegistryPlayerToolConstants.ItemPrefabName}' in ObjectDB.");
        return true;
    }

    private static bool ContainsPrefab(ZNetScene zNetScene, string prefabName)
    {
        return zNetScene.m_prefabs.Any(prefab => IsNamed(prefab, prefabName));
    }

    private static bool ContainsItem(ObjectDB objectDb, string prefabName)
    {
        return objectDb.m_items.Any(item => IsNamed(item, prefabName));
    }

    private static int FindPrefabIndex(IList<GameObject> prefabs, string prefabName)
    {
        for (var i = 0; i < prefabs.Count; i++)
        {
            if (IsNamed(prefabs[i], prefabName))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsNamed(GameObject? gameObject, string expectedName)
    {
        return gameObject != null && string.Equals(gameObject.name, expectedName, StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject? FindPrefabByName(ZNetScene zNetScene, string prefabName)
    {
        return zNetScene.m_prefabs.FirstOrDefault(prefab => IsNamed(prefab, prefabName));
    }

    private bool RegisterNamedPrefabIfPossible(ZNetScene zNetScene, GameObject itemPrefab)
    {
        try
        {
            var namedPrefabsField = typeof(ZNetScene).GetField(
                "m_namedPrefabs",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (namedPrefabsField?.GetValue(zNetScene) is not IDictionary namedPrefabs)
            {
                return false;
            }

            var prefabHash = GetStableHashCode(itemPrefab.name);
            if (namedPrefabs.Contains(prefabHash))
            {
                namedPrefabs[prefabHash] = itemPrefab;
            }
            else
            {
                namedPrefabs.Add(prefabHash, itemPrefab);
            }

            return true;
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Could not register '{RegistryPlayerToolConstants.ItemPrefabName}' in ZNetScene named prefab cache: " +
                exception.Message);
            return false;
        }
    }

    private void RegisterObjectDbItemHashIfPossible(ObjectDB objectDb, GameObject itemPrefab)
    {
        try
        {
            var itemByHashField = typeof(ObjectDB).GetField(
                "m_itemByHash",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (itemByHashField?.GetValue(objectDb) is not IDictionary itemByHash)
            {
                return;
            }

            var prefabHash = GetStableHashCode(itemPrefab.name);
            if (itemByHash.Contains(prefabHash))
            {
                itemByHash[prefabHash] = itemPrefab;
            }
            else
            {
                itemByHash.Add(prefabHash, itemPrefab);
            }
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Could not register '{RegistryPlayerToolConstants.ItemPrefabName}' in ObjectDB item hash cache: " +
                exception.Message);
        }
    }

    private void RefreshObjectDbItemHashes(ObjectDB objectDb)
    {
        try
        {
            var updateMethod = typeof(ObjectDB).GetMethod(
                "UpdateItemHashes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            updateMethod?.Invoke(objectDb, Array.Empty<object>());
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Could not refresh ObjectDB item hashes for '{RegistryPlayerToolConstants.ItemPrefabName}': " +
                exception.Message);
        }
    }

    private static int GetStableHashCode(string value)
    {
        unchecked
        {
            var hash1 = 5381;
            var hash2 = hash1;

            for (var i = 0; i < value.Length && value[i] != '\0'; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ value[i];

                if (i == value.Length - 1 || value[i + 1] == '\0')
                {
                    break;
                }

                hash2 = ((hash2 << 5) + hash2) ^ value[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }
}
