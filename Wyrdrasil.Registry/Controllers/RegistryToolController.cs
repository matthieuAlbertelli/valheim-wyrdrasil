using UnityEngine;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Registry.UI;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Controllers;

public sealed class RegistryToolController
{
    private const KeyCode ToggleKey = KeyCode.F8;
    private const KeyCode NextCategoryKey = KeyCode.F9;
    private const KeyCode NextActionKey = KeyCode.F10;

    private readonly RegistryModeService _modeService;
    private readonly ToolSelectionService _selectionService;
    private readonly ActionRegistry _actionRegistry;
    private readonly RegistryContext _actionContext;
    private readonly RegistryPersistenceService _persistenceService;
    private readonly FunctionalZoneService _zoneService;
    private readonly NavigationWaypointService _waypointService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly RegistryResidentService _residentService;
    private readonly WorldClockService _worldClockService;
    private readonly CraftStationAnchorEditorService _craftStationAnchorEditorService;
    private readonly ConstructionLinkVisualService _constructionLinkVisualService;
    private readonly RegistryHudRenderer _hudRenderer;

    public RegistryToolController(
        RegistryModeService modeService,
        ToolSelectionService selectionService,
        ActionRegistry actionRegistry,
        RegistryContext actionContext,
        RegistryPersistenceService persistenceService,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        RegistryResidentService residentService,
        WorldClockService worldClockService,
        CraftStationAnchorEditorService craftStationAnchorEditorService,
        ConstructionLinkVisualService constructionLinkVisualService,
        RegistryHudRenderer hudRenderer)
    {
        _modeService = modeService;
        _selectionService = selectionService;
        _actionRegistry = actionRegistry;
        _actionContext = actionContext;
        _persistenceService = persistenceService;
        _zoneService = zoneService;
        _waypointService = waypointService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _residentService = residentService;
        _worldClockService = worldClockService;
        _craftStationAnchorEditorService = craftStationAnchorEditorService;
        _constructionLinkVisualService = constructionLinkVisualService;
        _hudRenderer = hudRenderer;
    }

    public void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            if (_actionContext.ConstructionPlacementPreviewService.IsPreviewActive)
            {
                _actionContext.ConstructionPlacementPreviewService.CancelPreview();
            }

            _modeService.ToggleRegistryMode();
            ClearSelectionFeedbackVisuals();
            return;
        }

        if (!_modeService.IsRegistryModeEnabled)
        {
            ClearSelectionFeedbackVisuals();
            return;
        }

        var selectedAction = _modeService.State.SelectedAction;
        UpdateForceAssignFeedback();
        UpdateConstructionAssignmentFeedback(selectedAction);
        _zoneService.UpdateTargetedZoneHighlight();

        if (_actionContext.ConstructionPlacementPreviewService.IsPreviewActive)
        {
            UpdateConstructionPlacementPreview();
            return;
        }

        if (selectedAction == RegistryActionType.CreateTavernZone || selectedAction == RegistryActionType.CreateBedroomZone)
        {
            _zoneService.UpdatePendingZoneAuthoringPreview();
            HandleZoneAuthoringInputs();
        }

        if (Input.GetKeyDown(NextCategoryKey))
        {
            CancelInteractiveAuthoringIfNeeded();
            _selectionService.SelectNextCategory();
            return;
        }

        if (Input.GetKeyDown(NextActionKey))
        {
            CancelInteractiveAuthoringIfNeeded();
            _selectionService.SelectNextAction();
            return;
        }

        selectedAction = _modeService.State.SelectedAction;

        if (_craftStationAnchorEditorService.IsEditing)
        {
            _craftStationAnchorEditorService.Update(out var shouldSave);
            if (shouldSave)
            {
                _persistenceService.SaveWorldState();
            }

            return;
        }

        var isDeleteAction = IsDeleteAction(selectedAction);

        if (!isDeleteAction && Input.GetMouseButtonDown(0))
        {
            _actionRegistry.Execute(selectedAction, _actionContext);
            _persistenceService.SaveWorldState();
            return;
        }

        if (isDeleteAction && Input.GetMouseButtonDown(1))
        {
            _actionRegistry.Execute(selectedAction, _actionContext);
            _persistenceService.SaveWorldState();
        }
    }

    public void OnGUI()
    {
        if (!_modeService.IsRegistryModeEnabled)
        {
            return;
        }

        var constructionPlacementPreviewService = _actionContext.ConstructionPlacementPreviewService;

        _hudRenderer.Draw(
            _modeService.State,
            ToggleKey,
            NextCategoryKey,
            NextActionKey,
            _zoneService.Zones.Count,
            _waypointService.Waypoints.Count,
            _waypointService.PendingLinkStartWaypointId,
            _slotService.Slots.Count,
            _seatService.Seats.Count,
            _bedService.Beds.Count,
            _residentService.RegisteredNpcs.Count,
            _zoneService.GetPendingZoneAuthoringSnapshot(),
            _worldClockService.GetClockLabel(),
            _worldClockService.GetClockModeLabel(),
            _craftStationAnchorEditorService.IsEditing,
            _craftStationAnchorEditorService.StatusLabel,
            _craftStationAnchorEditorService.ControlsLabel,
            constructionPlacementPreviewService.IsPreviewActive,
            constructionPlacementPreviewService.StatusLabel,
            constructionPlacementPreviewService.ControlsLabel);
    }

    private void UpdateConstructionPlacementPreview()
    {
        var previewService = _actionContext.ConstructionPlacementPreviewService;
        var player = Player.m_localPlayer;

        if (_zoneService.TryGetPlacementPoint(out var placementPoint))
        {
            previewService.UpdatePreviewPosition(placementPoint);
        }
        else if (player != null)
        {
            previewService.UpdatePreviewPosition(player.transform.position + (player.transform.forward * 4f));
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            var adjustHeight = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (adjustHeight)
            {
                previewService.AdjustPreviewHeight(scrollDelta);
            }
            else
            {
                previewService.RotatePreview(scrollDelta);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            previewService.CancelPreview();
            _actionContext.Log.LogInfo("Cancelled construction placement preview.");
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (previewService.TryConfirmPreview(out var project, out var failureReason))
            {
                _actionContext.ConstructionDebugSessionService.SetLatestProjectId(project.Id);
                _actionContext.Log.LogInfo($"Confirmed construction placement preview. Created construction project {project.Id} with {project.Progress.TotalPieceCount} pieces and {project.WorkPosts.Count} work posts.");
                _persistenceService.SaveWorldState();
            }
            else
            {
                _actionContext.Log.LogWarning($"Failed to confirm construction placement preview: {failureReason}");
            }
        }
    }

    private void HandleZoneAuthoringInputs()
    {
        if (Input.GetMouseButtonDown(1))
        {
            _zoneService.HandleZoneAuthoringSecondaryInput();
            return;
        }

        if (_zoneService.IsZoneHeightEditingActive)
        {
            var scrollDelta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                var direction = scrollDelta > 0f ? 1 : -1;
                var adjustBase = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                _zoneService.AdjustPendingZoneHeight(direction, adjustBase);
            }
        }
    }

    private void CancelInteractiveAuthoringIfNeeded()
    {
        if (_actionContext.ConstructionPlacementPreviewService.IsPreviewActive)
        {
            _actionContext.ConstructionPlacementPreviewService.CancelPreview();
        }

        if (_zoneService.IsZoneAuthoringActive)
        {
            _zoneService.CancelPendingZoneAuthoring();
        }
    }

    private void UpdateForceAssignFeedback()
    {
        var state = _modeService.State;
        var hasPendingResident = state.SelectedAction == RegistryActionType.ForceAssignResident &&
                                 state.PendingResidentForceAssignId.HasValue;

        _residentService.SetPendingForceAssignResidentVisual(hasPendingResident ? state.PendingResidentForceAssignId : null);

        if (!hasPendingResident)
        {
            _slotService.SetPendingForceAssignTarget(null);
            _seatService.SetPendingForceAssignTarget(null);
            _bedService.SetPendingForceAssignTarget(null);
            return;
        }

        if (_slotService.TryGetSlotAtCrosshair(out var slotData))
        {
            _slotService.SetPendingForceAssignTarget(slotData.Id);
            _seatService.SetPendingForceAssignTarget(null);
            _bedService.SetPendingForceAssignTarget(null);
            return;
        }

        if (_seatService.TryGetSeatAtCrosshair(out var seatData))
        {
            _slotService.SetPendingForceAssignTarget(null);
            _seatService.SetPendingForceAssignTarget(seatData.Id);
            _bedService.SetPendingForceAssignTarget(null);
            return;
        }

        if (_bedService.TryGetBedAtCrosshair(out var bedData))
        {
            _slotService.SetPendingForceAssignTarget(null);
            _seatService.SetPendingForceAssignTarget(null);
            _bedService.SetPendingForceAssignTarget(bedData.Id);
            return;
        }

        if (_craftStationService.TryGetCraftStationAtCrosshair(out _))
        {
            _slotService.SetPendingForceAssignTarget(null);
            _seatService.SetPendingForceAssignTarget(null);
            _bedService.SetPendingForceAssignTarget(null);
            return;
        }

        _slotService.SetPendingForceAssignTarget(null);
        _seatService.SetPendingForceAssignTarget(null);
        _bedService.SetPendingForceAssignTarget(null);
    }

    private void UpdateConstructionAssignmentFeedback(RegistryActionType selectedAction)
    {
        _actionContext.ConstructionProjectMarkerService.SetHoveredProject(null);
        _craftStationService.SetPendingConstructionTarget(null);
        _residentService.SetPendingConstructionAssignmentResidentVisual(null);
        _constructionLinkVisualService.SetHoveredWorkbenchLink(null, null);
        _constructionLinkVisualService.SetHoveredResidentLink(null, null);

        if (selectedAction == RegistryActionType.AssignTargetCraftStationToConstructionProject)
        {
            var hasHoveredProject = _actionContext.ConstructionProjectMarkerService.TryGetTargetedProjectId(out var hoveredProjectId);
            if (hasHoveredProject)
            {
                _actionContext.ConstructionProjectMarkerService.SetHoveredProject(hoveredProjectId);
            }

            if (_actionContext.ConstructionDebugSessionService.TryGetPendingCraftStationProjectId(out var pendingProjectId))
            {
                if (!hasHoveredProject)
                {
                    _actionContext.ConstructionProjectMarkerService.SetHoveredProject(pendingProjectId);
                }

                if (_craftStationService.TryGetCraftStationAtCrosshair(out var craftStation))
                {
                    _craftStationService.SetPendingConstructionTarget(craftStation.Id);
                    _constructionLinkVisualService.SetHoveredWorkbenchLink(pendingProjectId, craftStation.Id);
                }
            }

            return;
        }

        if (selectedAction == RegistryActionType.AssignTargetResidentToLatestConstructionProject)
        {
            var hasHoveredProject = _actionContext.ConstructionProjectMarkerService.TryGetTargetedProjectId(out var hoveredProjectId);
            if (hasHoveredProject)
            {
                _actionContext.ConstructionProjectMarkerService.SetHoveredProject(hoveredProjectId);
            }

            if (_actionContext.ConstructionDebugSessionService.TryGetPendingResidentProjectId(out var pendingProjectId))
            {
                if (!hasHoveredProject)
                {
                    _actionContext.ConstructionProjectMarkerService.SetHoveredProject(pendingProjectId);
                }

                if (_residentService.TryGetTargetedRegisteredResident(out var resident))
                {
                    _residentService.SetPendingConstructionAssignmentResidentVisual(resident.Id);
                    _constructionLinkVisualService.SetHoveredResidentLink(pendingProjectId, resident.Id);
                }
            }
        }
    }

    private void ClearSelectionFeedbackVisuals()
    {
        _actionContext.ConstructionProjectMarkerService.SetHoveredProject(null);
        _craftStationService.SetPendingConstructionTarget(null);
        _residentService.SetPendingConstructionAssignmentResidentVisual(null);
        _constructionLinkVisualService.SetHoveredWorkbenchLink(null, null);
        _constructionLinkVisualService.SetHoveredResidentLink(null, null);
        _residentService.SetPendingForceAssignResidentVisual(null);
        _slotService.SetPendingForceAssignTarget(null);
        _seatService.SetPendingForceAssignTarget(null);
        _bedService.SetPendingForceAssignTarget(null);
    }

    private static bool IsDeleteAction(RegistryActionType actionType)
    {
        return actionType == RegistryActionType.DeleteZone ||
               actionType == RegistryActionType.DeleteSlot ||
               actionType == RegistryActionType.DeleteNavigationWaypoint ||
               actionType == RegistryActionType.DeleteDesignatedSeat ||
               actionType == RegistryActionType.DeleteDesignatedBed ||
               actionType == RegistryActionType.DeleteDesignatedCraftStation;
    }
}
