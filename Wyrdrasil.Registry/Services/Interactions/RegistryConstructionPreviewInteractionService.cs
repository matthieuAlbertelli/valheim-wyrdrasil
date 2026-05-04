using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Authoring;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistryConstructionPreviewInteractionService
{
    private readonly ManualLogSource _log;
    private readonly ISettlementsAuthoringApi _settlementsAuthoringApi;
    private readonly ConstructionPlacementPreviewService _constructionPlacementPreviewService;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;

    public RegistryConstructionPreviewInteractionService(
        ManualLogSource log,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        ConstructionPlacementPreviewService constructionPlacementPreviewService,
        ConstructionDebugSessionService constructionDebugSessionService)
    {
        _log = log;
        _settlementsAuthoringApi = settlementsAuthoringApi;
        _constructionPlacementPreviewService = constructionPlacementPreviewService;
        _constructionDebugSessionService = constructionDebugSessionService;
    }

    public bool IsPreviewActive => _constructionPlacementPreviewService.IsPreviewActive;

    public string StatusLabel => _constructionPlacementPreviewService.StatusLabel;

    public string ControlsLabel => _constructionPlacementPreviewService.ControlsLabel;

    public void CancelPreview()
    {
        _constructionPlacementPreviewService.CancelPreview();
    }

    public bool Update(out bool shouldSave)
    {
        shouldSave = false;

        var player = Player.m_localPlayer;

        if (_settlementsAuthoringApi.TryGetPlacementPoint(out var placementPoint))
        {
            _constructionPlacementPreviewService.UpdatePreviewPosition(placementPoint);
        }
        else if (player != null)
        {
            _constructionPlacementPreviewService.UpdatePreviewPosition(player.transform.position + (player.transform.forward * 4f));
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            var adjustHeight = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (adjustHeight)
            {
                _constructionPlacementPreviewService.AdjustPreviewHeight(scrollDelta);
            }
            else
            {
                _constructionPlacementPreviewService.RotatePreview(scrollDelta);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            _constructionPlacementPreviewService.CancelPreview();
            _log.LogInfo("Cancelled construction placement preview.");
            return true;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (_constructionPlacementPreviewService.TryConfirmPreview(out var project, out var failureReason))
        {
            _constructionDebugSessionService.SetLatestProjectId(project.Id);
            _log.LogInfo($"Confirmed construction placement preview. Created construction project {project.Id} with {project.Progress.TotalPieceCount} pieces and {project.WorkPosts.Count} work posts.");
            shouldSave = true;
            return true;
        }

        _log.LogWarning($"Failed to confirm construction placement preview: {failureReason}");
        return true;
    }
}
