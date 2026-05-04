using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Settlements.Runtime;

namespace Wyrdrasil.Registry.UI;

public sealed class RegistryHudStateProvider
{
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly RegistryResidentService _residentService;
    private readonly IRoutinesRuntimeApi _routinesRuntimeApi;
    private readonly CraftStationAnchorEditorService _craftStationAnchorEditorService;
    private readonly RegistryConstructionPreviewInteractionService _constructionPreviewInteractionService;
    private readonly RegistryZoneAuthoringInteractionService _zoneAuthoringInteractionService;
    private readonly RegistryInteractionModeRouter _interactionModeRouter;

    public RegistryHudStateProvider(
        ISettlementsRuntimeApi settlementsRuntimeApi,
        RegistryResidentService residentService,
        IRoutinesRuntimeApi routinesRuntimeApi,
        CraftStationAnchorEditorService craftStationAnchorEditorService,
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService,
        RegistryZoneAuthoringInteractionService zoneAuthoringInteractionService,
        RegistryInteractionModeRouter interactionModeRouter)
    {
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _residentService = residentService;
        _routinesRuntimeApi = routinesRuntimeApi;
        _craftStationAnchorEditorService = craftStationAnchorEditorService;
        _constructionPreviewInteractionService = constructionPreviewInteractionService;
        _zoneAuthoringInteractionService = zoneAuthoringInteractionService;
        _interactionModeRouter = interactionModeRouter;
    }

    public RegistryHudState Create(RegistryToolState state)
    {
        return new RegistryHudState(
            state,
            _interactionModeRouter.ResolveModeName(state),
            KeyCode.F8,
            KeyCode.F9,
            KeyCode.F10,
            _settlementsRuntimeApi.Zones.Count,
            _settlementsRuntimeApi.Waypoints.Count,
            _settlementsRuntimeApi.PendingLinkStartWaypointId,
            _settlementsRuntimeApi.Slots.Count,
            _settlementsRuntimeApi.Seats.Count,
            _settlementsRuntimeApi.Beds.Count,
            _residentService.RegisteredNpcs.Count,
            _zoneAuthoringInteractionService.CurrentSnapshot,
            _routinesRuntimeApi.GetClockLabel(),
            _routinesRuntimeApi.GetClockModeLabel(),
            _craftStationAnchorEditorService.IsEditing,
            _craftStationAnchorEditorService.StatusLabel,
            _craftStationAnchorEditorService.ControlsLabel,
            _constructionPreviewInteractionService.IsPreviewActive,
            _constructionPreviewInteractionService.StatusLabel,
            _constructionPreviewInteractionService.ControlsLabel);
    }
}
