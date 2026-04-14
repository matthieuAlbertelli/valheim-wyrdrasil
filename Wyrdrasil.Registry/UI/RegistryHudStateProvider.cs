using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.UI;

public sealed class RegistryHudStateProvider
{
    private readonly FunctionalZoneService _zoneService;
    private readonly NavigationWaypointService _waypointService;
    private readonly ZoneSlotService _slotService;
    private readonly SeatService _seatService;
    private readonly BedService _bedService;
    private readonly RegistryResidentService _residentService;
    private readonly WorldClockService _worldClockService;
    private readonly CraftStationAnchorEditorService _craftStationAnchorEditorService;
    private readonly RegistryConstructionPreviewInteractionService _constructionPreviewInteractionService;
    private readonly RegistryZoneAuthoringInteractionService _zoneAuthoringInteractionService;
    private readonly RegistryInteractionModeRouter _interactionModeRouter;

    public RegistryHudStateProvider(
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        RegistryResidentService residentService,
        WorldClockService worldClockService,
        CraftStationAnchorEditorService craftStationAnchorEditorService,
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService,
        RegistryZoneAuthoringInteractionService zoneAuthoringInteractionService,
        RegistryInteractionModeRouter interactionModeRouter)
    {
        _zoneService = zoneService;
        _waypointService = waypointService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _residentService = residentService;
        _worldClockService = worldClockService;
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
            _zoneService.Zones.Count,
            _waypointService.Waypoints.Count,
            _waypointService.PendingLinkStartWaypointId,
            _slotService.Slots.Count,
            _seatService.Seats.Count,
            _bedService.Beds.Count,
            _residentService.RegisteredNpcs.Count,
            _zoneAuthoringInteractionService.CurrentSnapshot,
            _worldClockService.GetClockLabel(),
            _worldClockService.GetClockModeLabel(),
            _craftStationAnchorEditorService.IsEditing,
            _craftStationAnchorEditorService.StatusLabel,
            _craftStationAnchorEditorService.ControlsLabel,
            _constructionPreviewInteractionService.IsPreviewActive,
            _constructionPreviewInteractionService.StatusLabel,
            _constructionPreviewInteractionService.ControlsLabel);
    }
}
