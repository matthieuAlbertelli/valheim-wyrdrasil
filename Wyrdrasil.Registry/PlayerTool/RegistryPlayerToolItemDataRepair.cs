using System;
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

    public static void RepairHumanoidEquipment(Humanoid? humanoid)
    {
        if (humanoid == null)
        {
            return;
        }

        var humanoidType = humanoid.GetType();
        foreach (var fieldName in HumanoidEquipmentFieldNames)
        {
            var field = FindInstanceField(humanoidType, fieldName);
            if (field?.GetValue(humanoid) is ItemDrop.ItemData itemData)
            {
                RepairItemData(itemData);
            }
        }
    }

    public static void RepairItemData(ItemDrop.ItemData? itemData)
    {
        if (itemData == null || !LooksLikeRegistryTool(itemData))
        {
            return;
        }

        var runtimePrefab = ResolveRuntimePrefab();
        if (runtimePrefab == null)
        {
            return;
        }

        itemData.m_dropPrefab = runtimePrefab;
    }

    public static GameObject? ResolveRuntimePrefab()
    {
        var objectDbPrefab = ObjectDB.instance?.GetItemPrefab(RegistryPlayerToolConstants.ItemPrefabName);
        if (objectDbPrefab != null)
        {
            return objectDbPrefab;
        }

        var zNetScene = ZNetScene.instance;
        if (zNetScene == null)
        {
            return null;
        }

        var zNetScenePrefab = zNetScene.GetPrefab(RegistryPlayerToolConstants.ItemPrefabName);
        if (zNetScenePrefab != null)
        {
            return zNetScenePrefab;
        }

        foreach (var prefab in zNetScene.m_prefabs)
        {
            if (prefab != null && string.Equals(prefab.name, RegistryPlayerToolConstants.ItemPrefabName, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private static bool LooksLikeRegistryTool(ItemDrop.ItemData itemData)
    {
        var sharedData = itemData.m_shared;
        if (sharedData == null)
        {
            return false;
        }

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

        var dropPrefab = itemData.m_dropPrefab;
        return dropPrefab != null &&
               string.Equals(dropPrefab.name, RegistryPlayerToolConstants.ItemPrefabName, StringComparison.OrdinalIgnoreCase);
    }

    private static FieldInfo? FindInstanceField(Type type, string fieldName)
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
}
