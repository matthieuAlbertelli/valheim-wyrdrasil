using UnityEngine;
using Wyrdrasil.Settlements.Authoring;

namespace Wyrdrasil.Registry.PlayerTool;

public sealed class RegistryPlayerToolRuntimeService
{
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly RegistryPlayerToolGameplayActionService _gameplayActionService;
    private readonly RegistryPlayerToolTargetFeedbackService _targetFeedbackService;

    public RegistryPlayerToolRuntimeService(
        ISettlementsAuthoringApi settlementsAuthoringApi,
        RegistryPlayerToolGameplayActionService gameplayActionService,
        RegistryPlayerToolTargetFeedbackService targetFeedbackService)
    {
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _gameplayActionService = gameplayActionService;
        _targetFeedbackService = targetFeedbackService;
    }

    public void Update()
    {
        _targetFeedbackService.Update();

        var player = Player.m_localPlayer;
        if (!RegistryPlayerToolSelectionService.TryGetSelectedActionPieceName(player, out var selectedActionPieceName))
        {
            _gameplayActionService.ClearPendingSubjectSilently();
            DisableAllPlayerSpatialVisualsAndCancelAuthoringIfNeeded();
            return;
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
