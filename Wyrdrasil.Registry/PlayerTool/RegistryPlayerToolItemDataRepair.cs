using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolItemDataRepair
{
    private static readonly string[] HumanoidEquipmentFieldNames =
    {
        "m_rightItem",
        "m_leftItem",
        "m_hiddenRightItem",
        "m_hiddenLeftItem",
        "m_chestItem",
        "m_legItem",
        "m_helmetItem",
        "m_shoulderItem",
        "m_utilityItem"
    };

    public static int RepairPlayerItems(Player? player)
    {
        if (player == null)
        {
            return 0;
        }

        var repaired = RepairHumanoidEquipment(player);
        repaired += RepairHumanoidInventory(player);
        return repaired;
    }

    public static int RepairHumanoidEquipment(Humanoid? humanoid)
    {
        if (humanoid == null)
        {
            return 0;
        }

        var repaired = 0;
        var humanoidType = humanoid.GetType();
        foreach (var fieldName in HumanoidEquipmentFieldNames)
        {
            var field = FindInstanceField(humanoidType, fieldName);
            if (field?.GetValue(humanoid) is ItemDrop.ItemData itemData && RepairItemData(itemData))
            {
                repaired++;
            }
        }

        return repaired;
    }

    public static int RepairHumanoidInventory(Humanoid? humanoid)
    {
        if (humanoid == null)
        {
            return 0;
        }

        if (!TryGetInventory(humanoid, out var inventory))
        {
            return 0;
        }

        return RepairInventory(inventory);
    }

    public static int RepairInventory(object? inventory)
    {
        if (inventory == null)
        {
            return 0;
        }

        var repaired = 0;
        foreach (var itemData in EnumerateInventoryItems(inventory))
        {
            if (RepairItemData(itemData))
            {
                repaired++;
            }
        }

        return repaired;
    }

    public static bool RepairItemDrop(ItemDrop? itemDrop)
    {
        return RepairItemDrop(itemDrop, ResolveRuntimePrefab());
    }

    public static bool RepairItemDrop(ItemDrop? itemDrop, GameObject? runtimePrefab)
    {
        if (itemDrop == null)
        {
            return false;
        }

        return RepairItemData(itemDrop.m_itemData, runtimePrefab, LooksLikeRegistryTool(itemDrop));
    }

    public static bool RepairItemData(ItemDrop.ItemData? itemData)
    {
        return RepairItemData(itemData, ResolveRuntimePrefab());
    }

    public static bool RepairItemData(ItemDrop.ItemData? itemData, GameObject? runtimePrefab)
    {
        return RepairItemData(itemData, runtimePrefab, false);
    }

    private static bool RepairItemData(ItemDrop.ItemData? itemData, GameObject? runtimePrefab, bool forceRegistryTool)
    {
        if (itemData == null || runtimePrefab == null || (!forceRegistryTool && !LooksLikeRegistryTool(itemData)))
        {
            return false;
        }

        var changed = false;
        if (itemData.m_dropPrefab != runtimePrefab)
        {
            itemData.m_dropPrefab = runtimePrefab;
            changed = true;
        }

        var runtimeItemDrop = runtimePrefab.GetComponent<ItemDrop>();
        var runtimeSharedData = runtimeItemDrop?.m_itemData.m_shared;
        if (runtimeSharedData != null && !ReferenceEquals(itemData.m_shared, runtimeSharedData))
        {
            itemData.m_shared = runtimeSharedData;
            changed = true;
        }

        if (itemData.m_shared != null)
        {
            if (!string.Equals(itemData.m_shared.m_name, RegistryPlayerToolConstants.DisplayName, StringComparison.Ordinal))
            {
                itemData.m_shared.m_name = RegistryPlayerToolConstants.DisplayName;
                changed = true;
            }

            if (!string.Equals(itemData.m_shared.m_description, RegistryPlayerToolConstants.Description, StringComparison.Ordinal))
            {
                itemData.m_shared.m_description = RegistryPlayerToolConstants.Description;
                changed = true;
            }

            var runtimePieceTable = runtimeSharedData?.m_buildPieces;
            if (runtimePieceTable != null && itemData.m_shared.m_buildPieces != runtimePieceTable)
            {
                itemData.m_shared.m_buildPieces = runtimePieceTable;
                changed = true;
            }
        }

        return changed;
    }

    public static GameObject? ResolveRuntimePrefab()
    {
        var objectDb = ObjectDB.instance;
        if (objectDb != null)
        {
            var objectDbPrefab = FindNamedPrefab(objectDb.m_items, RegistryPlayerToolConstants.ItemPrefabName);
            if (objectDbPrefab != null)
            {
                return objectDbPrefab;
            }
        }

        var zNetScene = ZNetScene.instance;
        if (zNetScene == null)
        {
            return null;
        }

        var zNetScenePrefab = FindNamedPrefab(zNetScene.m_prefabs, RegistryPlayerToolConstants.ItemPrefabName);
        if (zNetScenePrefab != null)
        {
            return zNetScenePrefab;
        }

        return TryResolveNamedPrefabWithoutLogging(zNetScene, RegistryPlayerToolConstants.ItemPrefabName);
    }

    public static bool LooksLikeRegistryTool(ItemDrop? itemDrop)
    {
        if (itemDrop == null)
        {
            return false;
        }

        if (LooksLikeRegistryTool(itemDrop.m_itemData))
        {
            return true;
        }

        return IsRegistryToolObjectName(itemDrop.name) ||
               IsRegistryToolObjectName(itemDrop.gameObject.name) ||
               IsRegistryToolObjectName(itemDrop.m_itemData.m_dropPrefab?.name);
    }

    public static bool LooksLikeRegistryTool(ItemDrop.ItemData? itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        var sharedData = itemData.m_shared;
        if (sharedData != null)
        {
            if (string.Equals(sharedData.m_name, RegistryPlayerToolConstants.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(sharedData.m_description, RegistryPlayerToolConstants.Description, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (sharedData.m_buildPieces != null &&
                string.Equals(sharedData.m_buildPieces.name, RegistryPlayerToolConstants.PieceTableName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        var dropPrefab = itemData.m_dropPrefab;
        return dropPrefab != null &&
               string.Equals(dropPrefab.name, RegistryPlayerToolConstants.ItemPrefabName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRegistryToolObjectName(string? objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        var normalizedName = objectName.Trim();
        if (normalizedName.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
        {
            normalizedName = normalizedName.Substring(0, normalizedName.Length - "(Clone)".Length).Trim();
        }

        return string.Equals(normalizedName, RegistryPlayerToolConstants.ItemPrefabName, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<ItemDrop.ItemData> EnumerateInventoryItems(object inventory)
    {
        var inventoryType = inventory.GetType();

        var getAllItemsMethod = FindInstanceMethod(inventoryType, "GetAllItems");
        if (getAllItemsMethod != null && getAllItemsMethod.GetParameters().Length == 0)
        {
            if (getAllItemsMethod.Invoke(inventory, Array.Empty<object>()) is IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is ItemDrop.ItemData itemData)
                    {
                        yield return itemData;
                    }
                }
            }
        }

        foreach (var fieldName in new[] { "m_inventory", "m_items" })
        {
            var field = FindInstanceField(inventoryType, fieldName);
            if (field?.GetValue(inventory) is not IEnumerable items)
            {
                continue;
            }

            foreach (var item in items)
            {
                if (item is ItemDrop.ItemData itemData)
                {
                    yield return itemData;
                }
            }
        }
    }

    private static bool TryGetInventory(Humanoid humanoid, out object inventory)
    {
        var humanoidType = humanoid.GetType();

        var getInventoryMethod = FindInstanceMethod(humanoidType, "GetInventory");
        if (getInventoryMethod != null && getInventoryMethod.GetParameters().Length == 0)
        {
            var methodValue = getInventoryMethod.Invoke(humanoid, Array.Empty<object>());
            if (methodValue != null)
            {
                inventory = methodValue;
                return true;
            }
        }

        var inventoryField = FindInstanceField(humanoidType, "m_inventory");
        if (inventoryField?.GetValue(humanoid) is { } fieldValue)
        {
            inventory = fieldValue;
            return true;
        }

        inventory = null!;
        return false;
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

    private static GameObject? TryResolveNamedPrefabWithoutLogging(ZNetScene zNetScene, string prefabName)
    {
        try
        {
            var namedPrefabsField = typeof(ZNetScene).GetField(
                "m_namedPrefabs",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (namedPrefabsField?.GetValue(zNetScene) is not IDictionary namedPrefabs)
            {
                return null;
            }

            var prefabHash = GetStableHashCode(prefabName);
            return namedPrefabs.Contains(prefabHash) ? namedPrefabs[prefabHash] as GameObject : null;
        }
        catch
        {
            return null;
        }
    }

    private static FieldInfo? FindInstanceField(Type? type, string fieldName)
    {
        while (type != typeof(object) && type != null)
        {
            var field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field != null)
            {
                return field;
            }

            type = type.BaseType;
        }

        return null;
    }

    private static MethodInfo? FindInstanceMethod(Type? type, string methodName)
    {
        while (type != typeof(object) && type != null)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    return method;
                }
            }

            type = type.BaseType;
        }

        return null;
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
