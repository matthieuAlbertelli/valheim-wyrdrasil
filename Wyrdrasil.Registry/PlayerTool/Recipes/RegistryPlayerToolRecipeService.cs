using System;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool.Recipes;

public sealed class RegistryPlayerToolRecipeService
{
    private readonly ManualLogSource _log;
    private ObjectDB? _registeredObjectDb;

    public RegistryPlayerToolRecipeService(ManualLogSource log)
    {
        _log = log;
    }

    public void EnsureRegistered(ObjectDB objectDb, ZNetScene zNetScene, GameObject itemPrefab)
    {
        if (_registeredObjectDb == objectDb && HasRecipe(objectDb))
        {
            return;
        }

        var itemDrop = itemPrefab.GetComponent<ItemDrop>();
        if (itemDrop == null)
        {
            _log.LogWarning($"Cannot register '{RegistryPlayerToolConstants.RecipeName}': item prefab has no ItemDrop component.");
            return;
        }

        var workbench = ResolveWorkbench(zNetScene);
        if (workbench == null)
        {
            _log.LogWarning($"Cannot register '{RegistryPlayerToolConstants.RecipeName}': workbench crafting station was not found.");
            return;
        }

        if (!TryCreateRequirement(objectDb, RegistryPlayerToolConstants.RecipeBronzePrefabName, 10, out var bronze) ||
            !TryCreateRequirement(objectDb, RegistryPlayerToolConstants.RecipeIronPrefabName, 10, out var iron) ||
            !TryCreateRequirement(objectDb, RegistryPlayerToolConstants.RecipeLeatherScrapsPrefabName, 6, out var leatherScraps))
        {
            return;
        }

        var existingIndex = FindRecipeIndex(objectDb);
        var recipe = ScriptableObject.CreateInstance<Recipe>();
        recipe.name = RegistryPlayerToolConstants.RecipeName;
        recipe.m_item = itemDrop;
        recipe.m_amount = 1;
        recipe.m_enabled = true;
        recipe.m_craftingStation = workbench;
        recipe.m_minStationLevel = RegistryPlayerToolConstants.RecipeWorkbenchLevel;
        recipe.m_resources = new[]
        {
            bronze,
            iron,
            leatherScraps
        };

        if (existingIndex >= 0)
        {
            var existingRecipe = objectDb.m_recipes[existingIndex];
            objectDb.m_recipes[existingIndex] = recipe;
            if (existingRecipe != null)
            {
                Object.Destroy(existingRecipe);
            }

            _log.LogInfo($"Replaced stale recipe '{RegistryPlayerToolConstants.RecipeName}' in ObjectDB.");
        }
        else
        {
            objectDb.m_recipes.Add(recipe);
            _log.LogInfo($"Registered recipe '{RegistryPlayerToolConstants.RecipeName}' in ObjectDB.");
        }

        _registeredObjectDb = objectDb;
    }

    private bool TryCreateRequirement(ObjectDB objectDb, string itemPrefabName, int amount, out Piece.Requirement requirement)
    {
        requirement = new Piece.Requirement();

        var resourcePrefab = objectDb.GetItemPrefab(itemPrefabName);
        var resourceItemDrop = resourcePrefab != null ? resourcePrefab.GetComponent<ItemDrop>() : null;
        if (resourceItemDrop == null)
        {
            _log.LogWarning(
                $"Cannot register '{RegistryPlayerToolConstants.RecipeName}': resource item '{itemPrefabName}' was not found.");
            return false;
        }

        requirement = new Piece.Requirement
        {
            m_resItem = resourceItemDrop,
            m_amount = amount,
            m_amountPerLevel = 0,
            m_recover = true
        };
        return true;
    }

    private static CraftingStation? ResolveWorkbench(ZNetScene zNetScene)
    {
        var workbenchPrefab = zNetScene.GetPrefab(RegistryPlayerToolConstants.CraftingStationPrefabName) ??
                              zNetScene.m_prefabs.FirstOrDefault(prefab =>
                                  prefab != null &&
                                  string.Equals(prefab.name, RegistryPlayerToolConstants.CraftingStationPrefabName, StringComparison.OrdinalIgnoreCase));

        return workbenchPrefab != null ? workbenchPrefab.GetComponent<CraftingStation>() : null;
    }

    private static bool HasRecipe(ObjectDB objectDb)
    {
        return FindRecipeIndex(objectDb) >= 0;
    }

    private static int FindRecipeIndex(ObjectDB objectDb)
    {
        for (var i = 0; i < objectDb.m_recipes.Count; i++)
        {
            var recipe = objectDb.m_recipes[i];
            if (recipe == null)
            {
                continue;
            }

            if (string.Equals(recipe.name, RegistryPlayerToolConstants.RecipeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(recipe.m_item?.name, RegistryPlayerToolConstants.ItemPrefabName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
