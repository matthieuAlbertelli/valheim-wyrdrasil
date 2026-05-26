using System;
using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolActionDefinitions
{
    public static readonly IReadOnlyList<RegistryPlayerToolActionDefinition> StaticActions = BuildStaticActions();

    public static IReadOnlyList<RegistryPlayerToolActionDefinition> All => StaticActions;

    public static IReadOnlyList<RegistryPlayerToolActionDefinition> BuildAll(
        IConstructionAuthoringApi? constructionAuthoringApi)
    {
        var actions = new List<RegistryPlayerToolActionDefinition>(StaticActions);
        if (constructionAuthoringApi == null)
        {
            return actions;
        }

        foreach (var blueprint in constructionAuthoringApi.GetBlueprints()
                     .Where(blueprint => !string.IsNullOrWhiteSpace(blueprint.Id))
                     .OrderBy(blueprint => blueprint.DisplayName)
                     .ThenBy(blueprint => blueprint.Id))
        {
            actions.Add(CreateBlueprintPlanAction(blueprint));
        }

        return actions;
    }

    public static RegistryPlayerToolActionDefinition CreateBlueprintPlanAction(StructureBlueprintData blueprint)
    {
        var displayName = string.IsNullOrWhiteSpace(blueprint.DisplayName)
            ? "Modèle sans nom"
            : blueprint.DisplayName;

        var description = blueprint.Pieces == null
            ? "Sélectionne ce modèle comme plan actif."
            : $"Sélectionne ce modèle comme plan actif ({blueprint.Pieces.Count} pièces).";

        return new RegistryPlayerToolActionDefinition(
            CreateBlueprintPlanActionPiecePrefabName(blueprint.Id),
            displayName,
            description,
            RegistryPlayerToolConstants.PlansCategoryIndex,
            blueprint.Id);
    }

    public static bool IsStaticActionPieceName(string piecePrefabName)
    {
        return StaticActions.Any(action =>
            string.Equals(action.PiecePrefabName, piecePrefabName, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsInspectActionPieceName(string piecePrefabName)
    {
        if (string.IsNullOrWhiteSpace(piecePrefabName))
        {
            return false;
        }

        if (string.Equals(
                piecePrefabName,
                RegistryPlayerToolConstants.InspectActionPiecePrefabName,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return piecePrefabName.StartsWith(
            RegistryPlayerToolConstants.InspectActionPiecePrefabNamePrefix,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAssignActionPieceName(string piecePrefabName)
    {
        return string.Equals(
            piecePrefabName,
            RegistryPlayerToolConstants.AssignActionPiecePrefabName,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBlueprintPlanActionPieceName(string piecePrefabName)
    {
        return !string.IsNullOrWhiteSpace(piecePrefabName) &&
               piecePrefabName.StartsWith(
                   RegistryPlayerToolConstants.BlueprintPlanActionPiecePrefabNamePrefix,
                   StringComparison.OrdinalIgnoreCase);
    }

    public static string CreateBlueprintPlanActionPiecePrefabName(string blueprintId)
    {
        return RegistryPlayerToolConstants.BlueprintPlanActionPiecePrefabNamePrefix +
               GetStableHashCode(blueprintId ?? string.Empty).ToString("X8");
    }

    private static IReadOnlyList<RegistryPlayerToolActionDefinition> BuildStaticActions()
    {
        var actions = new List<RegistryPlayerToolActionDefinition>();
        var categoryCount = Math.Max(1, RegistryPlayerToolConstants.PlayerToolCategoryLabels.Length);

        for (var categoryIndex = 0; categoryIndex < categoryCount; categoryIndex++)
        {
            actions.Add(CreateInspectAction(categoryIndex));

            if (categoryIndex == RegistryPlayerToolConstants.ActionsCategoryIndex)
            {
                AddMainActions(actions, categoryIndex);
            }
        }

        return actions;
    }

    private static RegistryPlayerToolActionDefinition CreateInspectAction(int categoryIndex)
    {
        return new RegistryPlayerToolActionDefinition(
            CreateInspectActionPiecePrefabName(categoryIndex),
            RegistryPlayerToolConstants.InspectActionDisplayName,
            RegistryPlayerToolConstants.InspectActionDescription,
            categoryIndex);
    }

    private static string CreateInspectActionPiecePrefabName(int categoryIndex)
    {
        if (categoryIndex == RegistryPlayerToolConstants.ActionsCategoryIndex)
        {
            return RegistryPlayerToolConstants.InspectActionPiecePrefabName;
        }

        return RegistryPlayerToolConstants.InspectActionPiecePrefabNamePrefix + categoryIndex;
    }

    private static void AddMainActions(ICollection<RegistryPlayerToolActionDefinition> actions, int categoryIndex)
    {
        actions.Add(new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.CreateTavernZoneActionPiecePrefabName,
            RegistryPlayerToolConstants.CreateTavernZoneActionDisplayName,
            RegistryPlayerToolConstants.CreateTavernZoneActionDescription,
            categoryIndex));

        actions.Add(new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.DesignateBedActionPiecePrefabName,
            RegistryPlayerToolConstants.DesignateBedActionDisplayName,
            RegistryPlayerToolConstants.DesignateBedActionDescription,
            categoryIndex));

        actions.Add(new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.SpawnAndRegisterVikingActionPiecePrefabName,
            RegistryPlayerToolConstants.SpawnAndRegisterVikingActionDisplayName,
            RegistryPlayerToolConstants.SpawnAndRegisterVikingActionDescription,
            categoryIndex));

        actions.Add(new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.AssignActionPiecePrefabName,
            RegistryPlayerToolConstants.AssignActionDisplayName,
            RegistryPlayerToolConstants.AssignActionDescription,
            categoryIndex));

        actions.Add(new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.DefineBuildingActionPiecePrefabName,
            RegistryPlayerToolConstants.DefineBuildingActionDisplayName,
            RegistryPlayerToolConstants.DefineBuildingActionDescription,
            categoryIndex));

        actions.Add(new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.CaptureBuildingBlueprintActionPiecePrefabName,
            RegistryPlayerToolConstants.CaptureBuildingBlueprintActionDisplayName,
            RegistryPlayerToolConstants.CaptureBuildingBlueprintActionDescription,
            categoryIndex));
    }

    private static uint GetStableHashCode(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }

            return hash;
        }
    }
}
