using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolSelectionService
{
    public static bool IsRegistryToolActive(Player? player)
    {
        return IsRegistryToolEquipped(player) &&
               TryGetPlayerBuildPieces(player, out var pieceTable) &&
               IsRegistryPieceTable(pieceTable);
    }

    public static bool IsRegistryToolEquipped(Player? player)
    {
        if (player == null)
        {
            return false;
        }

        return TryGetItemDataFieldValue(player, "m_rightItem", out var rightItem) && LooksLikeRegistryTool(rightItem) ||
               TryGetItemDataFieldValue(player, "m_leftItem", out var leftItem) && LooksLikeRegistryTool(leftItem);
    }

    public static bool IsSelectedAction(Player? player, string actionPiecePrefabName)
    {
        return TryGetSelectedActionPieceName(player, out var selectedActionPieceName) &&
               string.Equals(selectedActionPieceName, actionPiecePrefabName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetSelectedActionPieceName(Player? player, out string actionPieceName)
    {
        actionPieceName = string.Empty;

        if (!IsRegistryToolEquipped(player) ||
            !TryGetPlayerBuildPieces(player, out var pieceTable) ||
            !IsRegistryPieceTable(pieceTable))
        {
            return false;
        }

        // Prefer the PieceTable selection state over the placement ghost.
        // The ghost can persist from the previously selected pseudo-piece after the
        // native build menu has changed tabs, which made Inspect keep winning even
        // after selecting Assign Bed.
        if (TryGetSelectedPiecePrefab(pieceTable, out var selectedPiecePrefab) &&
            selectedPiecePrefab != null &&
            TryNormalizeKnownActionPieceName(selectedPiecePrefab.name, out actionPieceName))
        {
            return true;
        }

        // Fallback only: useful while Player.UpdatePlacement is running on Valheim
        // builds where the PieceTable selection fields are not readable.
        return TryGetSelectedActionPieceNameFromPlacementGhost(player, out actionPieceName);
    }

    public static bool TryGetPlayerBuildPieces(Player? player, out PieceTable pieceTable)
    {
        if (player == null)
        {
            pieceTable = null!;
            return false;
        }

        for (var type = player.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                "m_buildPieces",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field?.GetValue(player) is PieceTable currentPieceTable)
            {
                pieceTable = currentPieceTable;
                return true;
            }
        }

        pieceTable = null!;
        return false;
    }

    private static bool IsRegistryPieceTable(PieceTable? pieceTable)
    {
        return pieceTable != null &&
               string.Equals(
                   pieceTable.name,
                   RegistryPlayerToolConstants.PieceTableName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSelectedActionPieceNameFromPlacementGhost(Player? player, out string actionPieceName)
    {
        actionPieceName = string.Empty;
        if (player == null)
        {
            return false;
        }

        // Known vanilla field name first.
        if (TryGetGameObjectFieldValue(player, "m_placementGhost", out var placementGhost) &&
            TryNormalizeKnownActionPieceName(placementGhost.name, out actionPieceName))
        {
            return true;
        }

        // Defensive fallback for Valheim builds where the field name changes.
        // Only inspect fields that look placement-related to avoid accidentally using
        // unrelated equipped/visual GameObjects.
        for (var type = player.GetType(); type != null; type = type.BaseType)
        {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType != typeof(GameObject))
                {
                    continue;
                }

                if (!ContainsOrdinalIgnoreCase(field.Name, "placement") &&
                    !ContainsOrdinalIgnoreCase(field.Name, "ghost"))
                {
                    continue;
                }

                if (field.GetValue(player) is not GameObject gameObject || gameObject == null)
                {
                    continue;
                }

                if (TryNormalizeKnownActionPieceName(gameObject.name, out actionPieceName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetItemDataFieldValue(object target, string fieldName, out ItemDrop.ItemData itemData)
    {
        itemData = null!;

        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field?.GetValue(target) is ItemDrop.ItemData value && value != null)
            {
                itemData = value;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeRegistryTool(ItemDrop.ItemData itemData)
    {
        var sharedData = itemData.m_shared;
        if (sharedData == null)
        {
            return false;
        }

        if (IsRegistryToolNameOrLegacy(sharedData.m_name))
        {
            return true;
        }

        if (IsRegistryToolDescriptionOrLegacy(sharedData.m_description))
        {
            return true;
        }

        if (sharedData.m_buildPieces != null &&
            string.Equals(sharedData.m_buildPieces.name, RegistryPlayerToolConstants.PieceTableName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var dropPrefab = itemData.m_dropPrefab;
        return dropPrefab != null && IsRegistryToolPrefabName(dropPrefab.name);
    }


    private static bool IsRegistryToolNameOrLegacy(string? itemName)
    {
        return string.Equals(itemName, RegistryPlayerToolConstants.DisplayName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(itemName, RegistryPlayerToolConstants.LocalizedDisplayName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(itemName, RegistryPlayerToolConstants.LegacyDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRegistryToolDescriptionOrLegacy(string? description)
    {
        return string.Equals(description, RegistryPlayerToolConstants.Description, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(description, RegistryPlayerToolConstants.LocalizedDescription, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(description, RegistryPlayerToolConstants.LegacyDescription, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRegistryToolPrefabName(string? prefabName)
    {
        return string.Equals(prefabName, RegistryPlayerToolConstants.ItemPrefabName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(prefabName, RegistryPlayerToolConstants.LegacyItemPrefabName, StringComparison.OrdinalIgnoreCase);
    }


    private static bool TryGetGameObjectFieldValue(object target, string fieldName, out GameObject gameObject)
    {
        gameObject = null!;

        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field?.GetValue(target) is GameObject value && value != null)
            {
                gameObject = value;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSelectedPiecePrefab(PieceTable pieceTable, out GameObject selectedPiecePrefab)
    {
        selectedPiecePrefab = null!;

        // For the native build menu, Valheim stores the selected piece per selected
        // category. Resolve that state explicitly first. This is more reliable for
        // Wyrdrasil pseudo-actions than falling back to the global m_pieces order,
        // where index 0 is always Inspect.
        if (TryGetSelectedAvailablePiecePrefab(pieceTable, out selectedPiecePrefab))
        {
            return true;
        }

        // Defensive fallback for Valheim builds where the internal available-piece
        // cache is unavailable.
        if (TryGetSelectedPiecePrefabUsingNativeMethod(pieceTable, out selectedPiecePrefab))
        {
            return true;
        }

        if (pieceTable.m_pieces == null || pieceTable.m_pieces.Count == 0)
        {
            return false;
        }

        selectedPiecePrefab = pieceTable.m_pieces[0];
        return selectedPiecePrefab != null;
    }

    private static bool TryGetSelectedPiecePrefabUsingNativeMethod(PieceTable pieceTable, out GameObject selectedPiecePrefab)
    {
        selectedPiecePrefab = null!;

        var getSelectedPieceMethod = pieceTable.GetType().GetMethod(
            "GetSelectedPiece",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        if (getSelectedPieceMethod == null)
        {
            return false;
        }

        try
        {
            var selectedPiece = getSelectedPieceMethod.Invoke(pieceTable, null);
            if (TryResolvePiecePrefab(selectedPiece, out selectedPiecePrefab))
            {
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryGetSelectedAvailablePiecePrefab(PieceTable pieceTable, out GameObject selectedPiecePrefab)
    {
        selectedPiecePrefab = null!;

        var availablePieces = TryReadAvailablePieces(pieceTable);
        if (availablePieces == null || availablePieces.Count == 0)
        {
            return false;
        }

        var selectedCategoryIndex = TryReadSelectedCategoryIndex(pieceTable);
        if (selectedCategoryIndex < 0 || selectedCategoryIndex >= availablePieces.Count)
        {
            selectedCategoryIndex = 0;
        }

        if (availablePieces[selectedCategoryIndex] is not IList categoryPieces || categoryPieces.Count == 0)
        {
            return false;
        }

        var selectedPieceIndex = TryReadSelectedPieceIndexForCategory(pieceTable, selectedCategoryIndex, categoryPieces.Count);
        if (selectedPieceIndex < 0 || selectedPieceIndex >= categoryPieces.Count)
        {
            selectedPieceIndex = 0;
        }

        return TryResolvePiecePrefab(categoryPieces[selectedPieceIndex], out selectedPiecePrefab);
    }

    private static bool TryResolvePiecePrefab(object? candidate, out GameObject piecePrefab)
    {
        piecePrefab = null!;

        switch (candidate)
        {
            case GameObject gameObject when gameObject != null:
                piecePrefab = gameObject;
                return true;

            case Piece piece when piece != null:
                piecePrefab = piece.gameObject;
                return piecePrefab != null;

            case Component component when component != null:
                piecePrefab = component.gameObject;
                return piecePrefab != null;

            default:
                return false;
        }
    }

    private static IList? TryReadAvailablePieces(PieceTable pieceTable)
    {
        var availablePiecesField = pieceTable.GetType().GetField(
            "m_availablePieces",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return availablePiecesField?.GetValue(pieceTable) as IList;
    }

    private static int TryReadSelectedPieceIndexForCategory(PieceTable pieceTable, int categoryIndex, int categoryPieceCount)
    {
        try
        {
            var selectedPieceField = pieceTable.GetType().GetField(
                "m_selectedPiece",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var selectedPieceValue = selectedPieceField?.GetValue(pieceTable);
            if (selectedPieceValue is Vector2Int[] selectedPieces && selectedPieces.Length > 0)
            {
                if (categoryIndex < 0 || categoryIndex >= selectedPieces.Length)
                {
                    categoryIndex = 0;
                }

                var selectedCell = selectedPieces[categoryIndex];

                // Some Valheim builds store the absolute list index in x. Others store a
                // grid coordinate. With one action per category both resolve to 0, but this
                // keeps the resolver future-proof when we add more actions per tab.
                if (selectedCell.x >= 0 && selectedCell.x < categoryPieceCount)
                {
                    return selectedCell.x;
                }

                var gridIndex = selectedCell.x + (selectedCell.y * 15);
                if (gridIndex >= 0 && gridIndex < categoryPieceCount)
                {
                    return gridIndex;
                }

                return 0;
            }

            if (selectedPieceValue is int selectedPieceIndex)
            {
                return selectedPieceIndex;
            }
        }
        catch
        {
            return -1;
        }

        return -1;
    }

    private static int TryReadSelectedCategoryIndex(PieceTable pieceTable)
    {
        // Valheim's native GetSelectedCategory() returns the category value, not
        // necessarily the zero-based tab index. For vanilla enum-backed categories
        // the numeric enum value can differ from the index in m_categories.
        // Always map the category value back to the m_categories list before using
        // it to index m_availablePieces or m_selectedPiece.
        if (TryReadSelectedCategoryValueUsingNativeMethod(pieceTable, out var nativeCategoryValue) &&
            TryMapCategoryValueToListIndex(pieceTable, nativeCategoryValue, out var nativeCategoryIndex))
        {
            return nativeCategoryIndex;
        }

        var selectedCategoryField = pieceTable.GetType().GetField(
            "m_selectedCategory",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (selectedCategoryField == null)
        {
            return 0;
        }

        var selectedCategoryValue = selectedCategoryField.GetValue(pieceTable);
        if (TryMapCategoryValueToListIndex(pieceTable, selectedCategoryValue, out var fieldCategoryIndex))
        {
            return fieldCategoryIndex;
        }

        return 0;
    }

    private static bool TryReadSelectedCategoryValueUsingNativeMethod(
        PieceTable pieceTable,
        out object? selectedCategoryValue)
    {
        selectedCategoryValue = null;

        var getSelectedCategoryMethod = pieceTable.GetType().GetMethod(
            "GetSelectedCategory",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        if (getSelectedCategoryMethod == null)
        {
            return false;
        }

        try
        {
            selectedCategoryValue = getSelectedCategoryMethod.Invoke(pieceTable, null);
            return selectedCategoryValue != null;
        }
        catch
        {
            selectedCategoryValue = null;
            return false;
        }
    }

    private static bool TryMapCategoryValueToListIndex(
        PieceTable pieceTable,
        object? categoryValue,
        out int categoryIndex)
    {
        categoryIndex = 0;
        if (categoryValue == null)
        {
            return false;
        }

        var categoriesField = pieceTable.GetType().GetField(
            "m_categories",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (categoriesField?.GetValue(pieceTable) is IList categories)
        {
            for (var i = 0; i < categories.Count; i++)
            {
                if (AreCategoryValuesEquivalent(categories[i], categoryValue))
                {
                    categoryIndex = i;
                    return true;
                }
            }
        }

        if (categoryValue is int intValue)
        {
            categoryIndex = intValue;
            return true;
        }

        return false;
    }

    private static bool AreCategoryValuesEquivalent(object? left, object? right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        if (Equals(left, right))
        {
            return true;
        }

        if ((left.GetType().IsEnum || left is int) && (right.GetType().IsEnum || right is int))
        {
            return Convert.ToInt32(left) == Convert.ToInt32(right);
        }

        return false;
    }

    private static bool TryNormalizeKnownActionPieceName(string rawName, out string actionPieceName)
    {
        actionPieceName = NormalizeUnityObjectName(rawName);

        foreach (var actionDefinition in RegistryPlayerToolActionDefinitions.StaticActions)
        {
            if (string.Equals(actionPieceName, actionDefinition.PiecePrefabName, StringComparison.OrdinalIgnoreCase))
            {
                actionPieceName = actionDefinition.PiecePrefabName;
                return true;
            }
        }

        if (RegistryPlayerToolActionDefinitions.IsBlueprintPlanActionPieceName(actionPieceName))
        {
            return true;
        }

        actionPieceName = string.Empty;
        return false;
    }

    private static bool ContainsOrdinalIgnoreCase(string value, string search)
    {
        return !string.IsNullOrEmpty(value) &&
               !string.IsNullOrEmpty(search) &&
               value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeUnityObjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalized = name.Trim();
        const string cloneSuffix = "(Clone)";
        if (normalized.EndsWith(cloneSuffix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - cloneSuffix.Length).Trim();
        }

        return normalized;
    }
}
