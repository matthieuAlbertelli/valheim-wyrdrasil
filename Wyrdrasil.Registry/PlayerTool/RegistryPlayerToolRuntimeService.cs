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
            DisablePlayerZoneVisualsAndCancelAuthoringIfNeeded();
            return;
        }

        if (selectedActionPieceName != RegistryPlayerToolConstants.AssignBedActionPiecePrefabName)
        {
            _gameplayActionService.ClearPendingSubjectSilently();
        }

        if (selectedActionPieceName == RegistryPlayerToolConstants.CreateTavernZoneActionPiecePrefabName)
        {
            UpdatePlayerZoneAuthoringRuntime();
            return;
        }

        if (selectedActionPieceName == RegistryPlayerToolConstants.CaptureTavernBlueprintActionPiecePrefabName)
        {
            UpdatePlayerZoneSelectionRuntime();
            return;
        }

        DisablePlayerZoneVisualsAndCancelAuthoringIfNeeded();
    }

    private void UpdatePlayerZoneAuthoringRuntime()
    {
        // The player Registry tool must reuse the same polygonal zone authoring
        // system as the developer HUD action "Créer zone : Taverne". The dev
        // HUD makes those visuals visible through RegistryModeService; the player
        // tool keeps F8/dev mode separate, so it explicitly enables only the zone
        // authoring visuals while the tavern action is selected.
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

    private void UpdatePlayerZoneSelectionRuntime()
    {
        // Blueprint capture is a selection action, not an authoring action, but it
        // still needs the same existing zone visualization layer. Without this,
        // the player cannot physically designate the tavern zone to capture.
        if (_settlementsAuthoringApi.IsZoneAuthoringActive)
        {
            _settlementsAuthoringApi.CancelPendingZoneAuthoring();
        }

        _settlementsAuthoringApi.SetZoneAuthoringVisualsVisible(true);
        _settlementsAuthoringApi.UpdateTargetedZoneHighlight();
    }

    private void DisablePlayerZoneVisualsAndCancelAuthoringIfNeeded()
    {
        if (_settlementsAuthoringApi.IsZoneAuthoringActive)
        {
            _settlementsAuthoringApi.CancelPendingZoneAuthoring();
        }

        _settlementsAuthoringApi.SetZoneAuthoringVisualsVisible(false);
    }
}
