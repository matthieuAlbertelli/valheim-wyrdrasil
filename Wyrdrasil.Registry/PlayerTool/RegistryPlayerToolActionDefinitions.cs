using System;
using System.Collections.Generic;
using System.Linq;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolActionDefinitions
{
    public static readonly IReadOnlyList<RegistryPlayerToolActionDefinition> StaticActions = new[]
    {
        new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.InspectActionPiecePrefabName,
            RegistryPlayerToolConstants.InspectActionDisplayName,
            RegistryPlayerToolConstants.InspectActionDescription,
            categoryIndex: 0),

        new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.CreateTavernZoneActionPiecePrefabName,
            RegistryPlayerToolConstants.CreateTavernZoneActionDisplayName,
            RegistryPlayerToolConstants.CreateTavernZoneActionDescription,
            categoryIndex: 1),

        new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.DesignateBedActionPiecePrefabName,
            RegistryPlayerToolConstants.DesignateBedActionDisplayName,
            RegistryPlayerToolConstants.DesignateBedActionDescription,
            categoryIndex: 2),

        new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.SpawnAndRegisterVikingActionPiecePrefabName,
            RegistryPlayerToolConstants.SpawnAndRegisterVikingActionDisplayName,
            RegistryPlayerToolConstants.SpawnAndRegisterVikingActionDescription,
            categoryIndex: 3),

        new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.AssignBedActionPiecePrefabName,
            RegistryPlayerToolConstants.AssignBedActionDisplayName,
            RegistryPlayerToolConstants.AssignBedActionDescription,
            categoryIndex: 3),

        new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.DefineBuildingActionPiecePrefabName,
            RegistryPlayerToolConstants.DefineBuildingActionDisplayName,
            RegistryPlayerToolConstants.DefineBuildingActionDescription,
            categoryIndex: RegistryPlayerToolConstants.PlansCategoryIndex),

        new RegistryPlayerToolActionDefinition(
            RegistryPlayerToolConstants.CaptureBuildingBlueprintActionPiecePrefabName,
            RegistryPlayerToolConstants.CaptureBuildingBlueprintActionDisplayName,
            RegistryPlayerToolConstants.CaptureBuildingBlueprintActionDescription,
            categoryIndex: RegistryPlayerToolConstants.PlansCategoryIndex)
    };

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
