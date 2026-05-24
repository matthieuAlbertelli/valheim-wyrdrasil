using BepInEx.Logging;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolActionRouter
{
    private static ManualLogSource? _log;
    private static RegistryPlayerToolInspectionService? _inspectionService;
    private static RegistryPlayerToolGameplayActionService? _gameplayActionService;
    private static RegistryPlayerToolSaveService? _saveService;

    public static void Configure(
        ManualLogSource log,
        RegistryPlayerToolInspectionService inspectionService,
        RegistryPlayerToolGameplayActionService gameplayActionService,
        RegistryPlayerToolSaveService saveService)
    {
        _log = log;
        _inspectionService = inspectionService;
        _gameplayActionService = gameplayActionService;
        _saveService = saveService;
    }

    public static bool TryExecuteSelectedPrimaryAction(Player? player)
    {
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

        switch (selectedActionPieceName)
        {
            case RegistryPlayerToolConstants.InspectActionPiecePrefabName:
                if (_inspectionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; inspect action was consumed but not executed.");
                    return true;
                }

                _inspectionService.InspectCrosshairTarget();
                return true;

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

            case RegistryPlayerToolConstants.CaptureTavernBlueprintActionPiecePrefabName:
                if (_gameplayActionService == null)
                {
                    _log?.LogWarning("Registry player action router is not configured; tavern blueprint capture action was consumed but not executed.");
                    return true;
                }

                SaveIfPersistent(_gameplayActionService.CaptureTargetedTavernAsBlueprint(), "Enregistrer taverne");
                return true;

            default:
                return false;
        }
    }

    public static bool TryExecuteSelectedSecondaryAction(Player? player)
    {
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
