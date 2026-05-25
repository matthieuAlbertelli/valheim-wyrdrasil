using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Wyrdrasil.Construction.Authoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolPieceTableService
{
    private readonly ManualLogSource _log;
    private readonly IConstructionAuthoringApi _constructionAuthoringApi;
    private readonly RegistryPlayerToolBlueprintThumbnailService _blueprintThumbnailService;
    private readonly Dictionary<string, GameObject> _actionPiecePrefabsByName = new(StringComparer.OrdinalIgnoreCase);

    private GameObject? _pieceTableRoot;
    private PieceTable? _pieceTable;
    private ZNetScene? _registeredZNetScene;
    private bool _registeredAllActionNamedPrefabs;
    private string _builtActionSignature = string.Empty;

    public RegistryPlayerToolPieceTableService(
        ManualLogSource log,
        IConstructionAuthoringApi constructionAuthoringApi,
        RegistryPlayerToolBlueprintThumbnailService blueprintThumbnailService)
    {
        _log = log;
        _constructionAuthoringApi = constructionAuthoringApi;
        _blueprintThumbnailService = blueprintThumbnailService;
    }

    public PieceTable? GetOrCreate(ZNetScene zNetScene, PieceTable? sourcePieceTable)
    {
        var actionDefinitions = RegistryPlayerToolActionDefinitions.BuildAll(_constructionAuthoringApi);
        var expectedActionSignature = BuildActionSignature(actionDefinitions);
        if (_pieceTable != null)
        {
            if (string.Equals(_builtActionSignature, expectedActionSignature, StringComparison.Ordinal) &&
                _pieceTable.m_pieces != null &&
                _pieceTable.m_pieces.Count == actionDefinitions.Count)
            {
                EnsureActionPiecesRegistered(zNetScene);
                return _pieceTable;
            }

            _log.LogInfo(
                $"Refreshing '{RegistryPlayerToolConstants.PieceTableName}' because player tool actions changed. " +
                $"Built='{_builtActionSignature}', expected='{expectedActionSignature}'.");

            // Keep the PieceTable instance stable while the Registre is equipped. Valheim keeps
            // references to the active PieceTable in Player and in the equipped ItemData; destroying
            // and recreating the table can make the middle-mouse build panel stop opening until the
            // tool is unequipped/re-equipped. Now that we rebuild m_availablePieces manually, mutating
            // the existing dedicated table in place is the safer integration point.
            if (TryRefreshExistingPieceTable(zNetScene, sourcePieceTable, actionDefinitions, expectedActionSignature))
            {
                return _pieceTable;
            }

            ResetBuiltPieceTable();
        }

        if (sourcePieceTable == null)
        {
            _log.LogWarning(
                $"Cannot create '{RegistryPlayerToolConstants.PieceTableName}': source tool has no PieceTable.");
            return null;
        }

        _pieceTableRoot = Object.Instantiate(sourcePieceTable.gameObject);
        _pieceTableRoot.name = RegistryPlayerToolConstants.PieceTableName;
        _pieceTableRoot.SetActive(true);
        Object.DontDestroyOnLoad(_pieceTableRoot);

        var pieceTable = _pieceTableRoot.GetComponent<PieceTable>();
        if (pieceTable == null)
        {
            _log.LogWarning(
                $"Cannot create '{RegistryPlayerToolConstants.PieceTableName}': cloned source has no PieceTable component.");
            Object.Destroy(_pieceTableRoot);
            _pieceTableRoot = null;
            return null;
        }

        var registryCategories = ConfigureRegistryCategories(pieceTable, sourcePieceTable);
        var sourcePiecePrefab = sourcePieceTable.m_pieces.FirstOrDefault(piece => piece != null && piece.GetComponent<Piece>() != null);
        if (sourcePiecePrefab == null)
        {
            _log.LogWarning(
                $"Cannot create '{RegistryPlayerToolConstants.PieceTableName}': " +
                "source PieceTable does not contain any valid Piece prefab.");
            Object.Destroy(_pieceTableRoot);
            _pieceTableRoot = null;
            return null;
        }

        _log.LogInfo(
            $"Building '{RegistryPlayerToolConstants.PieceTableName}' from {actionDefinitions.Count} player action definitions.");

        var actionPiecePrefabs = new List<GameObject>();
        foreach (var actionDefinition in actionDefinitions)
        {
            var category = ResolveCategoryForAction(registryCategories, actionDefinition);
            var actionPiecePrefab = GetOrCreateActionPiecePrefab(zNetScene, sourcePiecePrefab, actionDefinition, category);
            if (actionPiecePrefab == null)
            {
                Object.Destroy(_pieceTableRoot);
                _pieceTableRoot = null;
                _actionPiecePrefabsByName.Clear();
                return null;
            }

            actionPiecePrefabs.Add(actionPiecePrefab);
            _actionPiecePrefabsByName[actionDefinition.PiecePrefabName] = actionPiecePrefab;
            _log.LogInfo(
                $"Added Registry player action piece '{actionDefinition.PiecePrefabName}' " +
                $"('{actionDefinition.DisplayName}') to category index {actionDefinition.CategoryIndex}.");
        }

        pieceTable.name = RegistryPlayerToolConstants.PieceTableName;
        pieceTable.m_pieces = actionPiecePrefabs;
        RefreshAvailablePieces(pieceTable);
        EnsureSelectedPieceStateMatchesAvailableCategories(pieceTable, registryCategories.Count);

        _pieceTable = pieceTable;
        _builtActionSignature = expectedActionSignature;

        EnsureActionPiecesRegistered(zNetScene);

        _log.LogInfo(
            $"Created dedicated player tool PieceTable '{RegistryPlayerToolConstants.PieceTableName}' " +
            $"with {actionPiecePrefabs.Count} Wyrdrasil action pieces.");

        return _pieceTable;
    }


    private bool TryRefreshExistingPieceTable(
        ZNetScene zNetScene,
        PieceTable? sourcePieceTable,
        IReadOnlyList<RegistryPlayerToolActionDefinition> actionDefinitions,
        string expectedActionSignature)
    {
        if (_pieceTable == null || _pieceTableRoot == null || sourcePieceTable == null)
        {
            return false;
        }

        var registryCategories = ConfigureRegistryCategories(_pieceTable, sourcePieceTable);
        var sourcePiecePrefab = sourcePieceTable.m_pieces.FirstOrDefault(piece => piece != null && piece.GetComponent<Piece>() != null);
        if (sourcePiecePrefab == null)
        {
            _log.LogWarning(
                $"Cannot refresh '{RegistryPlayerToolConstants.PieceTableName}': source PieceTable does not contain any valid Piece prefab.");
            return false;
        }

        var actionPiecePrefabs = new List<GameObject>(actionDefinitions.Count);
        foreach (var actionDefinition in actionDefinitions)
        {
            var category = ResolveCategoryForAction(registryCategories, actionDefinition);
            var actionPiecePrefab = GetOrCreateActionPiecePrefab(zNetScene, sourcePiecePrefab, actionDefinition, category);
            if (actionPiecePrefab == null)
            {
                return false;
            }

            actionPiecePrefabs.Add(actionPiecePrefab);
            _log.LogInfo(
                $"Available Registry player action piece '{actionDefinition.PiecePrefabName}' " +
                $"('{actionDefinition.DisplayName}') in category index {actionDefinition.CategoryIndex}.");
        }

        _pieceTable.name = RegistryPlayerToolConstants.PieceTableName;
        _pieceTable.m_pieces = actionPiecePrefabs;
        RefreshAvailablePieces(_pieceTable);
        EnsureSelectedPieceStateMatchesAvailableCategories(_pieceTable, registryCategories.Count);

        _builtActionSignature = expectedActionSignature;
        _registeredAllActionNamedPrefabs = false;
        EnsureActionPiecesRegistered(zNetScene);

        _log.LogInfo(
            $"Refreshed dedicated player tool PieceTable '{RegistryPlayerToolConstants.PieceTableName}' " +
            $"with {actionPiecePrefabs.Count} Wyrdrasil action pieces.");

        return true;
    }

    private static string BuildActionSignature(IReadOnlyList<RegistryPlayerToolActionDefinition> actionDefinitions)
    {
        return string.Join(
            "|",
            actionDefinitions.Select(action =>
                action.PiecePrefabName + ":" + action.DisplayName + ":" + action.Description + ":" + action.CategoryIndex + ":" + action.BlueprintId));
    }


    private void ResetBuiltPieceTable()
    {
        if (_pieceTableRoot != null)
        {
            Object.Destroy(_pieceTableRoot);
        }

        _pieceTableRoot = null;
        _pieceTable = null;
        _registeredZNetScene = null;
        _registeredAllActionNamedPrefabs = false;
        _builtActionSignature = string.Empty;
        _actionPiecePrefabsByName.Clear();
    }


    private void RefreshAvailablePieces(PieceTable pieceTable)
    {
        var updateAvailableMethod = pieceTable.GetType().GetMethod(
            "UpdateAvailable",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        if (updateAvailableMethod != null)
        {
            try
            {
                updateAvailableMethod.Invoke(pieceTable, null);

                var nativeAvailablePieces = GetListFieldValue(pieceTable, "m_availablePieces");
                if (AvailablePiecesCoverConfiguredCategories(pieceTable, nativeAvailablePieces))
                {
                    _log.LogInfo(
                        $"Refreshed '{RegistryPlayerToolConstants.PieceTableName}' available pieces with native PieceTable.UpdateAvailable: " +
                        $"source pieces={pieceTable.m_pieces.Count}, available categories={nativeAvailablePieces?.Count ?? 0}, " +
                        $"counts={FormatAvailablePieceCounts(nativeAvailablePieces)}.");
                    return;
                }

                _log.LogWarning(
                    $"Native PieceTable.UpdateAvailable produced an incomplete available-piece cache for " +
                    $"'{RegistryPlayerToolConstants.PieceTableName}'. Rebuilding it manually.");
            }
            catch (Exception exception)
            {
                _log.LogWarning(
                    $"Could not refresh available Wyrdrasil action pieces for '{RegistryPlayerToolConstants.PieceTableName}' " +
                    $"with native PieceTable.UpdateAvailable: {exception.Message}. Rebuilding manually.");
            }
        }
        else
        {
            _log.LogInfo(
                $"PieceTable.UpdateAvailable was not found for '{RegistryPlayerToolConstants.PieceTableName}'. " +
                "Rebuilding available Wyrdrasil action pieces manually.");
        }

        RebuildAvailablePiecesManually(pieceTable);
    }

    private void RebuildAvailablePiecesManually(PieceTable pieceTable)
    {
        var availablePiecesField = pieceTable.GetType().GetField(
            "m_availablePieces",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (availablePiecesField == null || !typeof(IList).IsAssignableFrom(availablePiecesField.FieldType))
        {
            _log.LogWarning(
                $"Could not manually rebuild available Wyrdrasil action pieces for " +
                $"'{RegistryPlayerToolConstants.PieceTableName}': m_availablePieces field was not found or is not a list.");
            return;
        }

        if (Activator.CreateInstance(availablePiecesField.FieldType) is not IList availablePieces)
        {
            _log.LogWarning(
                $"Could not manually rebuild available Wyrdrasil action pieces for " +
                $"'{RegistryPlayerToolConstants.PieceTableName}': m_availablePieces could not be created.");
            return;
        }

        var categoryListType = GetAvailablePieceCategoryListType(availablePiecesField.FieldType);
        if (categoryListType == null || !typeof(IList).IsAssignableFrom(categoryListType))
        {
            _log.LogWarning(
                $"Could not manually rebuild available Wyrdrasil action pieces for " +
                $"'{RegistryPlayerToolConstants.PieceTableName}': m_availablePieces category list type is unsupported.");
            return;
        }

        var maxCategoryIndex = GetMaximumKnownPieceCategoryIndex(pieceTable);
        for (var i = 0; i <= maxCategoryIndex; i++)
        {
            availablePieces.Add(CreateAvailablePieceCategoryList(categoryListType));
        }

        foreach (var piecePrefab in pieceTable.m_pieces)
        {
            if (piecePrefab == null)
            {
                continue;
            }

            var piece = piecePrefab.GetComponent<Piece>();
            if (piece == null)
            {
                continue;
            }

            var categoryIndex = Math.Max(0, GetPieceCategoryIndex(piece));
            EnsureAvailablePiecesCategorySlot(availablePieces, categoryListType, categoryIndex);

            if (availablePieces[categoryIndex] is IList categoryPieces)
            {
                AddPieceToAvailableCategory(categoryPieces, piece, piecePrefab);
            }
        }

        availablePiecesField.SetValue(pieceTable, availablePieces);

        _log.LogInfo(
            $"Manually rebuilt '{RegistryPlayerToolConstants.PieceTableName}' available pieces: " +
            $"source pieces={pieceTable.m_pieces.Count}, available categories={availablePieces.Count}, " +
            $"counts={FormatAvailablePieceCounts(availablePieces)}.");
    }

    private static bool AvailablePiecesCoverConfiguredCategories(PieceTable pieceTable, IList? availablePieces)
    {
        if (availablePieces == null || availablePieces.Count == 0)
        {
            return false;
        }

        var maxCategoryIndex = GetMaximumKnownPieceCategoryIndex(pieceTable);
        return availablePieces.Count > maxCategoryIndex;
    }

    private static Type? GetAvailablePieceCategoryListType(Type availablePiecesType)
    {
        if (availablePiecesType.IsGenericType)
        {
            return availablePiecesType.GetGenericArguments().FirstOrDefault();
        }

        return availablePiecesType
            .GetInterfaces()
            .Where(iface => iface.IsGenericType && iface.GetGenericArguments().Length == 1)
            .Select(iface => iface.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static IList CreateAvailablePieceCategoryList(Type categoryListType)
    {
        if (Activator.CreateInstance(categoryListType) is IList categoryPieces)
        {
            return categoryPieces;
        }

        return new ArrayList();
    }

    private static void EnsureAvailablePiecesCategorySlot(IList availablePieces, Type categoryListType, int categoryIndex)
    {
        while (availablePieces.Count <= categoryIndex)
        {
            availablePieces.Add(CreateAvailablePieceCategoryList(categoryListType));
        }
    }

    private static void AddPieceToAvailableCategory(IList categoryPieces, Piece piece, GameObject piecePrefab)
    {
        var itemType = GetCollectionItemType(categoryPieces.GetType());
        if (itemType == null || itemType.IsInstanceOfType(piece))
        {
            categoryPieces.Add(piece);
            return;
        }

        if (itemType.IsInstanceOfType(piecePrefab))
        {
            categoryPieces.Add(piecePrefab);
        }
    }

    private static Type? GetCollectionItemType(Type collectionType)
    {
        if (collectionType.IsGenericType && collectionType.GetGenericArguments().Length == 1)
        {
            return collectionType.GetGenericArguments()[0];
        }

        return collectionType
            .GetInterfaces()
            .Where(iface => iface.IsGenericType && iface.GetGenericArguments().Length == 1)
            .Select(iface => iface.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    private static int GetMaximumKnownPieceCategoryIndex(PieceTable pieceTable)
    {
        var maxCategoryIndex = 0;

        foreach (var enumValue in Enum.GetValues(typeof(Piece.PieceCategory)))
        {
            maxCategoryIndex = Math.Max(maxCategoryIndex, Convert.ToInt32(enumValue));
        }

        var categories = GetListFieldValue(pieceTable, "m_categories");
        if (categories != null)
        {
            for (var i = 0; i < categories.Count; i++)
            {
                if (categories[i] != null)
                {
                    maxCategoryIndex = Math.Max(maxCategoryIndex, Convert.ToInt32(categories[i]));
                }
            }
        }

        foreach (var piecePrefab in pieceTable.m_pieces)
        {
            if (piecePrefab == null)
            {
                continue;
            }

            var piece = piecePrefab.GetComponent<Piece>();
            if (piece == null)
            {
                continue;
            }

            maxCategoryIndex = Math.Max(maxCategoryIndex, GetPieceCategoryIndex(piece));
        }

        return Math.Max(maxCategoryIndex, RegistryPlayerToolConstants.PlayerToolCategoryLabels.Length - 1);
    }

    private static int GetPieceCategoryIndex(Piece piece)
    {
        var field = piece.GetType().GetField(
            "m_category",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field?.GetValue(piece) is { } categoryValue)
        {
            return Convert.ToInt32(categoryValue);
        }

        return 0;
    }

    private static string FormatAvailablePieceCounts(IList? availablePieces)
    {
        if (availablePieces == null || availablePieces.Count == 0)
        {
            return "none";
        }

        var counts = new List<string>(availablePieces.Count);
        for (var i = 0; i < availablePieces.Count; i++)
        {
            if (availablePieces[i] is ICollection collection)
            {
                counts.Add($"{i}:{collection.Count}");
            }
            else
            {
                counts.Add($"{i}:?");
            }
        }

        return string.Join(",", counts);
    }

    private static void EnsureSelectedPieceStateMatchesAvailableCategories(PieceTable pieceTable, int configuredCategoryCount)
    {
        var availablePieces = GetListFieldValue(pieceTable, "m_availablePieces");
        var selectedPieceCount = Math.Max(
            Math.Max(configuredCategoryCount, availablePieces?.Count ?? 0),
            GetMaximumKnownPieceCategoryIndex(pieceTable) + 1);
        if (selectedPieceCount <= 0)
        {
            selectedPieceCount = 1;
        }

        SetVector2IntArrayFieldIfPresent(pieceTable, "m_selectedPiece", selectedPieceCount);
        SetVector2IntArrayFieldIfPresent(pieceTable, "m_lastSelectedPiece", selectedPieceCount);
        ClampIntFieldIfPresent(pieceTable, "m_selectedCategory", 0, Math.Max(0, configuredCategoryCount - 1));
    }

    private static void SetVector2IntArrayFieldIfPresent(object target, string fieldName, int length)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null || field.FieldType != typeof(Vector2Int[]))
        {
            return;
        }

        var values = new Vector2Int[length];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = Vector2Int.zero;
        }

        field.SetValue(target, values);
    }

    private static void ClampIntFieldIfPresent(object target, string fieldName, int minValue, int maxValue)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null || field.FieldType != typeof(int))
        {
            return;
        }

        var value = (int)field.GetValue(target);
        var clamped = Math.Max(minValue, Math.Min(maxValue, value));
        if (clamped != value)
        {
            field.SetValue(target, clamped);
        }
    }

    private GameObject? GetOrCreateActionPiecePrefab(
        ZNetScene zNetScene,
        GameObject sourcePiecePrefab,
        RegistryPlayerToolActionDefinition actionDefinition,
        object? category)
    {
        if (_pieceTableRoot == null)
        {
            return null;
        }

        if (_actionPiecePrefabsByName.TryGetValue(actionDefinition.PiecePrefabName, out var existingActionPiecePrefab) &&
            existingActionPiecePrefab != null)
        {
            ConfigurePieceMetadata(existingActionPiecePrefab, actionDefinition, category, zNetScene);
            DisableWorldVisualization(existingActionPiecePrefab);
            return existingActionPiecePrefab;
        }

        var actionPiecePrefab = Object.Instantiate(sourcePiecePrefab, _pieceTableRoot.transform, false);
        actionPiecePrefab.name = actionDefinition.PiecePrefabName;
        actionPiecePrefab.SetActive(true);

        ConfigurePieceMetadata(actionPiecePrefab, actionDefinition, category, zNetScene);
        DisableWorldVisualization(actionPiecePrefab);
        _actionPiecePrefabsByName[actionDefinition.PiecePrefabName] = actionPiecePrefab;

        return actionPiecePrefab;
    }

    private IReadOnlyList<object?> ConfigureRegistryCategories(PieceTable targetPieceTable, PieceTable sourcePieceTable)
    {
        var sourceCategories = GetListFieldValue(sourcePieceTable, "m_categories");
        var targetCategories = GetListFieldValue(targetPieceTable, "m_categories");
        var targetLabels = GetListFieldValue(targetPieceTable, "m_categoryLabels");

        if (sourceCategories == null || sourceCategories.Count == 0 || targetCategories == null || targetLabels == null)
        {
            _log.LogWarning(
                $"Could not fully configure player tool categories for '{RegistryPlayerToolConstants.PieceTableName}'. " +
                "The native build menu will keep the source tool category setup.");
            return Array.Empty<object?>();
        }

        targetCategories.Clear();
        targetLabels.Clear();

        var labelCount = RegistryPlayerToolConstants.PlayerToolCategoryLabels.Length;
        var categoryCount = Math.Min(sourceCategories.Count, labelCount);
        var registryCategories = new List<object?>(categoryCount);
        for (var i = 0; i < categoryCount; i++)
        {
            var category = sourceCategories[i];
            targetCategories.Add(category!);
            targetLabels.Add(RegistryPlayerToolConstants.PlayerToolCategoryLabels[i]);
            registryCategories.Add(category);
        }

        return registryCategories;
    }

    private static object? ResolveCategoryForAction(
        IReadOnlyList<object?> registryCategories,
        RegistryPlayerToolActionDefinition actionDefinition)
    {
        if (registryCategories.Count == 0)
        {
            return null;
        }

        if (actionDefinition.CategoryIndex >= 0 && actionDefinition.CategoryIndex < registryCategories.Count)
        {
            return registryCategories[actionDefinition.CategoryIndex];
        }

        return registryCategories[0];
    }

    private static IList? GetListFieldValue(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return field?.GetValue(target) as IList;
    }

    private static Vector2Int[] CreateSelectedPieceState(Vector2Int[]? sourceSelectedPiece, int categoryCount)
    {
        var selectedPieceCount = Math.Max(categoryCount, sourceSelectedPiece?.Length ?? 0);
        if (selectedPieceCount <= 0)
        {
            selectedPieceCount = 1;
        }

        var copy = new Vector2Int[selectedPieceCount];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = Vector2Int.zero;
        }

        return copy;
    }

    private void ConfigurePieceMetadata(
        GameObject actionPiecePrefab,
        RegistryPlayerToolActionDefinition actionDefinition,
        object? category,
        ZNetScene zNetScene)
    {
        var piece = actionPiecePrefab.GetComponent<Piece>();
        if (piece == null)
        {
            return;
        }

        SetFieldIfPresent(piece, "m_name", actionDefinition.DisplayName);
        SetFieldIfPresent(piece, "m_description", actionDefinition.Description);
        SetPieceCategoryIfPresent(piece, category);
        ClearArrayFieldIfPresent(piece, "m_resources");

        if (actionDefinition.IsBlueprintPlan)
        {
            var thumbnailSprite = _blueprintThumbnailService.TryGetThumbnail(actionDefinition.BlueprintId, zNetScene);
            if (thumbnailSprite != null)
            {
                SetFieldIfPresent(piece, "m_icon", thumbnailSprite);
            }
        }
    }

    private static void SetPieceCategoryIfPresent(Piece piece, object? categoryValue)
    {
        if (categoryValue == null)
        {
            return;
        }

        var field = piece.GetType().GetField(
            "m_category",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null && field.FieldType.IsInstanceOfType(categoryValue))
        {
            field.SetValue(piece, categoryValue);
        }
    }

    private void EnsureActionPiecesRegistered(ZNetScene zNetScene)
    {
        if (_actionPiecePrefabsByName.Count == 0)
        {
            return;
        }

        foreach (var actionPiecePrefab in _actionPiecePrefabsByName.Values)
        {
            if (!ContainsPrefab(zNetScene, actionPiecePrefab.name))
            {
                zNetScene.m_prefabs.Add(actionPiecePrefab);
                _log.LogInfo($"Registered '{actionPiecePrefab.name}' in ZNetScene.");
            }
        }

        if (_registeredZNetScene == zNetScene && _registeredAllActionNamedPrefabs)
        {
            return;
        }

        _registeredAllActionNamedPrefabs = true;
        foreach (var actionPiecePrefab in _actionPiecePrefabsByName.Values)
        {
            _registeredAllActionNamedPrefabs &= RegisterNamedPrefabIfPossible(zNetScene, actionPiecePrefab);
        }

        _registeredZNetScene = zNetScene;
    }

    private static void DisableWorldVisualization(GameObject actionPiecePrefab)
    {
        foreach (var renderer in actionPiecePrefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        foreach (var collider in actionPiecePrefab.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }

    private static bool ContainsPrefab(ZNetScene zNetScene, string prefabName)
    {
        return zNetScene.m_prefabs.Any(prefab => IsNamed(prefab, prefabName));
    }

    private static bool IsNamed(GameObject? gameObject, string expectedName)
    {
        return gameObject != null && string.Equals(gameObject.name, expectedName, StringComparison.OrdinalIgnoreCase);
    }

    private bool RegisterNamedPrefabIfPossible(ZNetScene zNetScene, GameObject prefab)
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

            var prefabHash = GetStableHashCode(prefab.name);
            if (namedPrefabs.Contains(prefabHash))
            {
                namedPrefabs[prefabHash] = prefab;
            }
            else
            {
                namedPrefabs.Add(prefabHash, prefab);
            }

            return true;
        }
        catch (Exception exception)
        {
            _log.LogWarning(
                $"Could not register '{prefab.name}' in ZNetScene named prefab cache: " +
                exception.Message);
            return false;
        }
    }

    private static void SetFieldIfPresent(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field != null && field.FieldType.IsInstanceOfType(value))
        {
            field.SetValue(target, value);
        }
    }

    private static void ClearArrayFieldIfPresent(object target, string fieldName)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (field == null || !field.FieldType.IsArray)
        {
            return;
        }

        var emptyArray = Array.CreateInstance(field.FieldType.GetElementType()!, 0);
        field.SetValue(target, emptyArray);
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
