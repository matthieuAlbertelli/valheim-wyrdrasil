using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Wyrdrasil.Registry.PlayerTool;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.Patches;

/// <summary>
/// Resolves saved Registry tool items during the character/profile loading phase.
///
/// Valheim can deserialize the player inventory before Wyrdrasil's normal runtime bootstrap has
/// had a chance to create and register the full Registry tool prefab in ZNetScene/ObjectDB. If a
/// previously saved inventory contains an item whose saved prefab name is "WyrdrasilRegistry",
/// ObjectDB.GetItemPrefab logs "Failed to find item prefab WyrdrasilRegistry" before the world
/// is even loaded.
///
/// This patch provides a minimal early ObjectDB-only placeholder cloned from the vanilla Hammer.
/// The normal RegistryPlayerToolItemService later replaces/refreshes it with the complete runtime
/// prefab, including the current Wyrdrasil PieceTable, and repairs the loaded item instance.
/// </summary>
[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.GetItemPrefab), typeof(string))]
internal static class WyrdrasilRegistryObjectDbGetItemPrefabPatch
{
    private static GameObject? _hiddenRoot;
    private static GameObject? _earlyPrefab;

    private static bool Prefix(ObjectDB __instance, string __0, ref GameObject __result)
    {
        if (!string.Equals(__0, RegistryPlayerToolConstants.ItemPrefabName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var existing = FindNamedPrefab(__instance.m_items, RegistryPlayerToolConstants.ItemPrefabName);
        if (existing != null)
        {
            __result = existing;
            return false;
        }

        if (_earlyPrefab != null)
        {
            EnsureRegistered(__instance, _earlyPrefab);
            __result = _earlyPrefab;
            return false;
        }

        var sourcePrefab = FindNamedPrefab(__instance.m_items, RegistryPlayerToolConstants.SourceItemPrefabName);
        if (sourcePrefab == null)
        {
            // Fall back to Valheim's native lookup. This keeps the patch defensive if ObjectDB is
            // not fully initialized yet or if the vanilla Hammer prefab changes name in the future.
            return true;
        }

        var sourceItemDrop = sourcePrefab.GetComponent<ItemDrop>();
        if (sourceItemDrop == null)
        {
            return true;
        }

        EnsureHiddenRoot();

        var prefab = Object.Instantiate(sourcePrefab, _hiddenRoot!.transform, false);
        prefab.name = RegistryPlayerToolConstants.ItemPrefabName;
        prefab.SetActive(true);

        var itemDrop = prefab.GetComponent<ItemDrop>();
        if (itemDrop == null)
        {
            Object.Destroy(prefab);
            return true;
        }

        ConfigureEarlyRegistryItemData(prefab, itemDrop, sourceItemDrop);

        _earlyPrefab = prefab;
        EnsureRegistered(__instance, prefab);
        __result = prefab;
        return false;
    }

    private static void ConfigureEarlyRegistryItemData(
        GameObject prefab,
        ItemDrop itemDrop,
        ItemDrop sourceItemDrop)
    {
        itemDrop.m_itemData.m_dropPrefab = prefab;

        var sharedData = itemDrop.m_itemData.m_shared;
        sharedData.m_name = RegistryPlayerToolConstants.DisplayName;
        sharedData.m_description = RegistryPlayerToolConstants.Description;

        // At character selection time the Wyrdrasil PieceTable may not exist yet. Keep the source
        // hammer table temporarily; the full item service swaps this to WyrdrasilRegistryPieceTable
        // as soon as ZNetScene/ObjectDB are both ready in the world scene.
        if (sharedData.m_buildPieces == null)
        {
            sharedData.m_buildPieces = sourceItemDrop.m_itemData.m_shared.m_buildPieces;
        }

        var binder = prefab.GetComponent<RegistryPlayerToolItemDropPrefabBinder>();
        if (binder == null)
        {
            binder = prefab.AddComponent<RegistryPlayerToolItemDropPrefabBinder>();
        }

        binder.RuntimePrefab = prefab;
        binder.BindDropPrefab();
    }

    private static void EnsureRegistered(ObjectDB objectDb, GameObject prefab)
    {
        var index = FindPrefabIndex(objectDb.m_items, RegistryPlayerToolConstants.ItemPrefabName);
        if (index >= 0)
        {
            objectDb.m_items[index] = prefab;
        }
        else
        {
            objectDb.m_items.Add(prefab);
        }

        RegisterObjectDbItemHashIfPossible(objectDb, prefab);
        RefreshObjectDbItemHashes(objectDb);
    }

    private static void EnsureHiddenRoot()
    {
        if (_hiddenRoot != null)
        {
            return;
        }

        _hiddenRoot = new GameObject("Wyrdrasil.Registry.PlayerTool.EarlyObjectDbPrefabs");
        _hiddenRoot.SetActive(false);
        Object.DontDestroyOnLoad(_hiddenRoot);
    }

    private static GameObject? FindNamedPrefab(IEnumerable<GameObject?> prefabs, string prefabName)
    {
        foreach (var prefab in prefabs)
        {
            if (prefab != null && string.Equals(prefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private static int FindPrefabIndex(IList<GameObject> prefabs, string prefabName)
    {
        for (var i = 0; i < prefabs.Count; i++)
        {
            var prefab = prefabs[i];
            if (prefab != null && string.Equals(prefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static void RegisterObjectDbItemHashIfPossible(ObjectDB objectDb, GameObject itemPrefab)
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
        catch
        {
            // Best-effort cache update. The prefix itself still guarantees name-based lookup for
            // the Registry prefab, so failures here should not block profile loading.
        }
    }

    private static void RefreshObjectDbItemHashes(ObjectDB objectDb)
    {
        try
        {
            var updateMethod = typeof(ObjectDB).GetMethod(
                "UpdateItemHashes",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            updateMethod?.Invoke(objectDb, Array.Empty<object>());
        }
        catch
        {
            // Best-effort only.
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
