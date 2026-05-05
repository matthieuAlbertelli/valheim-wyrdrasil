using Wyrdrasil.Construction.Services;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Services.Interactions;

public sealed class RegistrySelectionFeedbackService
{
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly CraftStationService _craftStationService;
    private readonly RegistryResidentService _residentService;
    private readonly ConstructionProjectMarkerService _constructionProjectMarkerService;
    private readonly ConstructionDebugSessionService _constructionDebugSessionService;
    private readonly ConstructionLinkVisualService _constructionLinkVisualService;

    public RegistrySelectionFeedbackService(
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        RegistryResidentService residentService,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionDebugSessionService constructionDebugSessionService,
        ConstructionLinkVisualService constructionLinkVisualService)
    {
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _craftStationService = craftStationService;
        _residentService = residentService;
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _constructionDebugSessionService = constructionDebugSessionService;
        _constructionLinkVisualService = constructionLinkVisualService;
    }

    public void Update(RegistryToolState state)
    {
        UpdateForceAssignFeedback(state);
        UpdateConstructionAssignmentFeedback(state.SelectedAction);
    }

    public void ClearAll()
    {
        _constructionProjectMarkerService.SetHoveredProject(null);
        _craftStationService.SetPendingConstructionTarget(null);
        _residentService.SetPendingConstructionAssignmentResidentVisual(null);
        _constructionLinkVisualService.SetHoveredWorkbenchLink(null, null);
        _constructionLinkVisualService.SetHoveredResidentLink(null, null);
        _residentService.SetPendingForceAssignResidentVisual(null);
        _slotService.SetPendingForceAssignTarget(null);
        _seatService.SetPendingForceAssignTarget(null);
        _bedService.SetPendingForceAssignTarget(null);
    }

    private void UpdateForceAssignFeedback(RegistryToolState state)
    {
        var isResidentAnchorAssignmentAction = state.SelectedAction == RegistryActionType.ForceAssignResident ||
                                               state.SelectedAction == RegistryActionType.AssignCraftStation;
        var hasPendingResident = isResidentAnchorAssignmentAction &&
                                 state.PendingResidentForceAssignId.HasValue;

        _residentService.SetPendingForceAssignResidentVisual(hasPendingResident ? state.PendingResidentForceAssignId : null);

        if (!hasPendingResident)
        {
            _slotService.SetPendingForceAssignTarget(null);
            _seatService.SetPendingForceAssignTarget(null);
            _bedService.SetPendingForceAssignTarget(null);
            return;
        }

        if (state.SelectedAction == RegistryActionType.AssignCraftStation)
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
        _constructionProjectMarkerService.SetHoveredProject(null);
        _craftStationService.SetPendingConstructionTarget(null);
        _residentService.SetPendingConstructionAssignmentResidentVisual(null);
        _constructionLinkVisualService.SetHoveredWorkbenchLink(null, null);
        _constructionLinkVisualService.SetHoveredResidentLink(null, null);

        if (selectedAction == RegistryActionType.AssignTargetCraftStationToConstructionProject)
        {
            var hasHoveredProject = _constructionProjectMarkerService.TryGetTargetedProjectId(out var hoveredProjectId);
            if (hasHoveredProject)
            {
                _constructionProjectMarkerService.SetHoveredProject(hoveredProjectId);
            }

            if (_constructionDebugSessionService.TryGetPendingCraftStationProjectId(out var pendingProjectId))
            {
                if (!hasHoveredProject)
                {
                    _constructionProjectMarkerService.SetHoveredProject(pendingProjectId);
                }

                if (_craftStationService.TryGetCraftStationAtCrosshair(out var craftStation))
                {
                    _craftStationService.SetPendingConstructionTarget(craftStation.Id);
                    _constructionLinkVisualService.SetHoveredWorkbenchLink(pendingProjectId, craftStation.Id);
                }
            }

            return;
        }

        if (selectedAction != RegistryActionType.AssignTargetResidentToLatestConstructionProject)
        {
            return;
        }

        var hasHoveredResidentProject = _constructionProjectMarkerService.TryGetTargetedProjectId(out var hoveredResidentProjectId);
        if (hasHoveredResidentProject)
        {
            _constructionProjectMarkerService.SetHoveredProject(hoveredResidentProjectId);
        }

        if (!_constructionDebugSessionService.TryGetPendingResidentProjectId(out var pendingResidentProjectId))
        {
            return;
        }

        if (!hasHoveredResidentProject)
        {
            _constructionProjectMarkerService.SetHoveredProject(pendingResidentProjectId);
        }

        if (_residentService.TryGetTargetedRegisteredResident(out var resident))
        {
            _residentService.SetPendingConstructionAssignmentResidentVisual(resident.Id);
            _constructionLinkVisualService.SetHoveredResidentLink(pendingResidentProjectId, resident.Id);
        }
    }
}
