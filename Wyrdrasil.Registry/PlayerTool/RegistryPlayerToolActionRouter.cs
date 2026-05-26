using BepInEx.Logging;
using Wyrdrasil.Registry.Services.Interactions;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolActionRouter
{
    private static ManualLogSource? _log;
    private static RegistryPlayerToolInspectionService? _inspectionService;
    private static RegistryPlayerToolGameplayActionService? _gameplayActionService;
    private static RegistryPlayerToolSaveService? _saveService;
    private static RegistryConstructionPreviewInteractionService? _constructionPreviewInteractionService;

    public static void Configure(
        ManualLogSource log,
        RegistryPlayerToolInspectionService inspectionService,
        RegistryPlayerToolGameplayActionService gameplayActionService,
        RegistryPlayerToolSaveService saveService,
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService)
    {
        _log = log;
        _inspectionService = inspectionService;
        _gameplayActionService = gameplayActionService;
        _saveService = saveService;
        _constructionPreviewInteractionService = constructionPreviewInteractionService;
    }

    public static bool IsConstructionPreviewActive => _constructionPreviewInteractionService?.IsPreviewActive == true;

    public static bool TryExecuteSelectedPrimaryAction(Player? player)
    {
        if (IsConstructionPreviewActive)
        {
            return true;
        }

        if (!RegistryPlayerToolSelectionService.TryGetSelectedActionPieceName(player, out var selectedActionPieceName))
        {
            return false;
        }

        if (RegistryPlayerToolActionDefinitions.IsBlueprintPlanActionPieceName(selectedActionPieceName))
        {
            if (_gameplayActionService == null)
            {
                _log?.LogWarning("Registry player action router is not configured; blueprint selection action was consumed but not executed.");
                return true;
            }

            _gameplayActionService.SelectBlueprintPlan(selectedActionPieceName);
            return true;
        }

        if (RegistryPlayerToolActionDefinitions.IsInspectActionPieceName(selectedActionPieceName))
        {
            if (_inspectionService == null)
            {
                _log?.LogWarning("Registry player action router is not configured; inspect action was consumed but not executed.");
                return true;
            }

            _inspectionService.InspectCrosshairTarget();
            return true;
        }

        switch (selectedActionPieceName)
        {
            case RegistryPlayerToolConstants.SpawnAndRegisterVikingActionPiecePrefabName:
                if (_gameplayActionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; spawn and register viking action was consumed but not executed.");
                    return true;
                }

                SaveIfPersistent(_gameplayActionService.SpawnAndRegisterViking(), "Appeler viking");
                return true;

            case RegistryPlayerToolConstants.CreateTavernZoneActionPiecePrefabName:
                if (_gameplayActionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; tavern zone action was consumed but not executed.");
                    return true;
                }

                SaveIfPersistent(_gameplayActionService.CreateTavernZoneAtCrosshair(), "Délimiter taverne");
                return true;

            case RegistryPlayerToolConstants.DesignateBedActionPiecePrefabName:
                if (_gameplayActionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; bed designation action was consumed but not executed.");
                    return true;
                }

                SaveIfPersistent(_gameplayActionService.DesignateBedAtCrosshair(), "Marquer lit");
                return true;

            case RegistryPlayerToolConstants.AssignBedActionPiecePrefabName:
                if (_gameplayActionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; bed assignment action was consumed but not executed.");
                    return true;
                }

                SaveIfPersistent(_gameplayActionService.AssignBedAtCrosshair(), "Assigner lit");
                return true;

            case RegistryPlayerToolConstants.DefineBuildingActionPiecePrefabName:
                if (_gameplayActionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; building definition action was consumed but not executed.");
                    return true;
                }

                SaveIfPersistent(_gameplayActionService.DefineBuildingAtCrosshair(), "Délimiter bâtiment");
                return true;

            case RegistryPlayerToolConstants.CaptureBuildingBlueprintActionPiecePrefabName:
                if (_gameplayActionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; building blueprint capture action was consumed but not executed.");
                    return true;
                }

                SaveIfPersistent(_gameplayActionService.CaptureTargetedBuildingAsBlueprint(), "Enregistrer bâtiment");
                return true;

            default:
                return false;
        }
    }

    public static bool TryExecuteSelectedSecondaryAction(Player? player)
    {
        if (IsConstructionPreviewActive)
        {
            return true;
        }

        if (!RegistryPlayerToolSelectionService.TryGetSelectedActionPieceName(player, out var selectedActionPieceName))
        {
            return false;
        }

        if (_gameplayActionService == null)
        {
            _log?.LogWarning("Registry player action router is not configured; secondary action was consumed but not executed.");
            return true;
        }

        switch (selectedActionPieceName)
        {
            case RegistryPlayerToolConstants.SpawnAndRegisterVikingActionPiecePrefabName:
                SaveIfPersistent(_gameplayActionService.KillTargetedRegisteredViking(), "Tuer viking enregistré");
                return true;

            case RegistryPlayerToolConstants.DesignateBedActionPiecePrefabName:
                SaveIfPersistent(_gameplayActionService.RemoveDesignatedBedAtCrosshair(), "Retirer lit marqué");
                return true;

            case RegistryPlayerToolConstants.AssignBedActionPiecePrefabName:
                _gameplayActionService.ClearPendingSubject();
                return true;

            case RegistryPlayerToolConstants.CreateTavernZoneActionPiecePrefabName:
                SaveIfPersistent(_gameplayActionService.HandleTavernZoneSecondaryInput(), "Annuler tracé taverne");
                return true;

            case RegistryPlayerToolConstants.DefineBuildingActionPiecePrefabName:
                SaveIfPersistent(_gameplayActionService.HandleBuildingSecondaryInput(), "Annuler tracé bâtiment");
                return true;

            default:
                return true;
        }
    }


    private static void SaveIfPersistent(bool shouldSave, string actionName)
    {
        if (!shouldSave)
        {
            return;
        }

        if (_saveService == null)
        {
            _log?.LogWarning($"Registry player action '{actionName}' changed persistent state, but save service is not configured.");
            return;
        }

        _saveService.SaveAfterPersistentPlayerAction(actionName);
    }
}
