using UnityEngine;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Settlements.Authoring;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolRuntimeService
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly RegistryPlayerToolGameplayActionService _gameplayActionService;
    private readonly RegistryPlayerToolTargetFeedbackService _targetFeedbackService;
    private readonly RegistryPlayerToolInspectionRevealService _inspectionRevealService;
    private readonly RegistryConstructionPreviewInteractionService _constructionPreviewInteractionService;
    private readonly RegistryPlayerToolSaveService _saveService;

    private string _lastObservedActionPieceName = string.Empty;
    private string _lastPreviewStartedFromActionPieceName = string.Empty;

    public RegistryPlayerToolRuntimeService(
        ISettlementsAuthoringApi settlementsAuthoringApi,
        RegistryPlayerToolGameplayActionService gameplayActionService,
        RegistryPlayerToolTargetFeedbackService targetFeedbackService,
        RegistryPlayerToolInspectionRevealService inspectionRevealService,
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService,
        RegistryPlayerToolSaveService saveService)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _gameplayActionService = gameplayActionService;
        _targetFeedbackService = targetFeedbackService;
        _inspectionRevealService = inspectionRevealService;
        _constructionPreviewInteractionService = constructionPreviewInteractionService;
        _saveService = saveService;
    }

    public void Update()
    {
        _targetFeedbackService.Update();
        _inspectionRevealService.Update();

        var player = Player.m_localPlayer;
        if (!RegistryPlayerToolSelectionService.IsRegistryToolEquipped(player))
        {
            ResetPlayerToolRuntimeState(cancelPreview: true);
            return;
        }

        if (!RegistryPlayerToolSelectionService.TryGetSelectedActionPieceName(player, out var selectedActionPieceName))
        {
            _lastObservedActionPieceName = string.Empty;
            _lastPreviewStartedFromActionPieceName = string.Empty;
            _inspectionRevealService.SetRevealVisible(false);
            _gameplayActionService.ClearPendingSubjectSilently();
            if (_constructionPreviewInteractionService.IsPreviewActive)
            {
                UpdateConstructionPreviewRuntime();
            }
            else
            {
                DisableAllPlayerSpatialVisualsAndCancelAuthoringIfNeeded();
            }
            return;
        }

        var selectionChanged = !string.Equals(
            selectedActionPieceName,
            _lastObservedActionPieceName,
            System.StringComparison.OrdinalIgnoreCase);
        _lastObservedActionPieceName = selectedActionPieceName;

        if (selectionChanged)
        {
            CancelRuntimeStateOwnedByPreviousAction(selectedActionPieceName);
        }

        if (_constructionPreviewInteractionService.IsPreviewActive)
        {
            _inspectionRevealService.SetRevealVisible(false);
            DisableAllPlayerSpatialVisualsAndCancelAuthoringIfNeeded();
            UpdateConstructionPreviewRuntime();
            return;
        }

        if (RegistryPlayerToolActionDefinitions.IsInspectActionPieceName(selectedActionPieceName))
        {
            _lastPreviewStartedFromActionPieceName = string.Empty;
            _gameplayActionService.ClearPendingSubjectSilently();
            _inspectionRevealService.SetRevealVisible(true);
            return;
        }

        _inspectionRevealService.SetRevealVisible(false);

        if (RegistryPlayerToolActionDefinitions.IsBlueprintPlanActionPieceName(selectedActionPieceName))
        {
            _gameplayActionService.ClearPendingSubjectSilently();
            DisableAllPlayerSpatialVisualsAndCancelAuthoringIfNeeded();

            if (selectionChanged ||
                !string.Equals(
                    _lastPreviewStartedFromActionPieceName,
                    selectedActionPieceName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                _lastPreviewStartedFromActionPieceName = selectedActionPieceName;
                _gameplayActionService.SelectBlueprintPlan(selectedActionPieceName);
            }

            return;
        }

        if (selectionChanged)
        {
            _lastPreviewStartedFromActionPieceName = string.Empty;
        }

        if (selectedActionPieceName != RegistryPlayerToolConstants.AssignBedActionPiecePrefabName)
        {
            _gameplayActionService.ClearPendingSubjectSilently();
        }

        if (selectedActionPieceName == RegistryPlayerToolConstants.CreateTavernZoneActionPiecePrefabName)
        {
            DisablePlayerBuildingVisualsAndCancelAuthoringIfNeeded();
            UpdatePlayerZoneAuthoringRuntime();
            return;
        }

        if (selectedActionPieceName == RegistryPlayerToolConstants.DefineBuildingActionPiecePrefabName)
        {
            DisablePlayerZoneVisualsAndCancelAuthoringIfNeeded();
            UpdatePlayerBuildingAuthoringRuntime();
            return;
        }

        if (selectedActionPieceName == RegistryPlayerToolConstants.CaptureBuildingBlueprintActionPiecePrefabName)
        {
            DisablePlayerZoneVisualsAndCancelAuthoringIfNeeded();
            UpdatePlayerBuildingSelectionRuntime();
            return;
        }

        DisableAllPlayerSpatialVisualsAndCancelAuthoringIfNeeded();
    }

    private void ResetPlayerToolRuntimeState(bool cancelPreview)
    {
        _lastObservedActionPieceName = string.Empty;
        _lastPreviewStartedFromActionPieceName = string.Empty;
        _inspectionRevealService.SetRevealVisible(false);
        CancelPlayerWorldInteractionState(cancelPreview, clearPendingSubject: true);
    }

    private void CancelRuntimeStateOwnedByPreviousAction(string selectedActionPieceName)
    {
        var previousPreviewBelongsToSelectedAction =
            !string.IsNullOrWhiteSpace(_lastPreviewStartedFromActionPieceName) &&
            string.Equals(
                _lastPreviewStartedFromActionPieceName,
                selectedActionPieceName,
                System.StringComparison.OrdinalIgnoreCase);

        if (_constructionPreviewInteractionService.IsPreviewActive && !previousPreviewBelongsToSelectedAction)
        {
            _lastPreviewStartedFromActionPieceName = string.Empty;
            CancelPlayerWorldInteractionState(cancelPreview: true, clearPendingSubject: true);
            return;
        }

        CancelPlayerWorldInteractionState(cancelPreview: false, clearPendingSubject: false);
    }

    private void CancelPlayerWorldInteractionState(bool cancelPreview, bool clearPendingSubject)
    {
        if (clearPendingSubject)
        {
            _gameplayActionService.ClearPendingSubjectSilently();
        }

        if (cancelPreview && _constructionPreviewInteractionService.IsPreviewActive)
        {
            _constructionPreviewInteractionService.CancelPreview();
        }

        DisableAllPlayerSpatialVisualsAndCancelAuthoringIfNeeded();
    }

    private void UpdateConstructionPreviewRuntime()
    {
        if (RegistryPlayerToolPlacementInterceptor.IsGameplayInputBlocked())
        {
            return;
        }

        _constructionPreviewInteractionService.Update(out var shouldSave);
        if (shouldSave)
        {
            _saveService.SaveAfterPersistentPlayerAction("Lancer chantier");
        }
    }

    private void UpdatePlayerZoneAuthoringRuntime()
    {
        _settlementsAuthoringApi.SetZoneAuthoringVisualsVisible(true);
        _settlementsAuthoringApi.UpdatePendingZoneAuthoringPreview();
        _settlementsAuthoringApi.UpdateTargetedZoneHighlight();

        if (!_settlementsAuthoringApi.IsZoneHeightEditingActive)
        {
            return;
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) <= 0.01f)
        {
            return;
        }

        var direction = scrollDelta > 0f ? 1 : -1;
        var adjustBase = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        _settlementsAuthoringApi.AdjustPendingZoneHeight(direction, adjustBase);
    }

    private void UpdatePlayerBuildingAuthoringRuntime()
    {
        _settlementsAuthoringApi.SetBuildingAuthoringVisualsVisible(true);
        _settlementsAuthoringApi.UpdatePendingBuildingAuthoringPreview();
        _settlementsAuthoringApi.UpdateTargetedBuildingHighlight();

        if (!_settlementsAuthoringApi.IsBuildingHeightEditingActive)
        {
            return;
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) <= 0.01f)
        {
            return;
        }

        var direction = scrollDelta > 0f ? 1 : -1;
        var adjustBase = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        _settlementsAuthoringApi.AdjustPendingBuildingHeight(direction, adjustBase);
    }

    private void UpdatePlayerBuildingSelectionRuntime()
    {
        if (_settlementsAuthoringApi.IsBuildingAuthoringActive)
        {
            _settlementsAuthoringApi.CancelPendingBuildingAuthoring();
        }

        _settlementsAuthoringApi.SetBuildingAuthoringVisualsVisible(true);
        _settlementsAuthoringApi.UpdateTargetedBuildingHighlight();
    }

    private void DisableAllPlayerSpatialVisualsAndCancelAuthoringIfNeeded()
    {
        DisablePlayerZoneVisualsAndCancelAuthoringIfNeeded();
        DisablePlayerBuildingVisualsAndCancelAuthoringIfNeeded();
    }

    private void DisablePlayerZoneVisualsAndCancelAuthoringIfNeeded()
    {
        if (_settlementsAuthoringApi.IsZoneAuthoringActive)
        {
            _settlementsAuthoringApi.CancelPendingZoneAuthoring();
        }

        _settlementsAuthoringApi.SetZoneAuthoringVisualsVisible(false);
    }

    private void DisablePlayerBuildingVisualsAndCancelAuthoringIfNeeded()
    {
        if (_settlementsAuthoringApi.IsBuildingAuthoringActive)
        {
            _settlementsAuthoringApi.CancelPendingBuildingAuthoring();
        }

        _settlementsAuthoringApi.SetBuildingAuthoringVisualsVisible(false);
    }
}
