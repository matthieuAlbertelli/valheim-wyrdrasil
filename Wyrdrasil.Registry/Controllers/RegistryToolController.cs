using UnityEngine;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Registry.UI;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Controllers;

public sealed class RegistryToolController
{
    private const KeyCode ToggleKey = KeyCode.F8;
    private const KeyCode NextCategoryKey = KeyCode.F9;
    private const KeyCode NextActionKey = KeyCode.F10;

    private readonly RegistryModeService _modeService;
    private readonly RegistrySelectionService _selectionService;
    private readonly RegistryActionRegistry _actionRegistry;
    private readonly RegistryContext _actionContext;
    private readonly RegistryPersistenceService _persistenceService;
    private readonly RegistryZoneService _zoneService;
    private readonly RegistryWaypointService _waypointService;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryBedService _bedService;
    private readonly RegistryResidentService _residentService;
    private readonly RegistryWorldClockService _worldClockService;
    private readonly RegistryHudRenderer _hudRenderer;

    public RegistryToolController(
        RegistryModeService modeService,
        RegistrySelectionService selectionService,
        RegistryActionRegistry actionRegistry,
        RegistryContext actionContext,
        RegistryPersistenceService persistenceService,
        RegistryZoneService zoneService,
        RegistryWaypointService waypointService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryBedService bedService,
        RegistryResidentService residentService,
        RegistryWorldClockService worldClockService,
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
        _residentService = residentService;
        _worldClockService = worldClockService;
        _hudRenderer = hudRenderer;
    }

    public void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            _modeService.ToggleRegistryMode();
            return;
        }

        if (!_modeService.IsRegistryModeEnabled)
        {
            return;
        }

        var selectedAction = _modeService.State.SelectedAction;
        UpdateForceAssignFeedback();

        if (selectedAction == RegistryActionType.CreateTavernZone || selectedAction == RegistryActionType.CreateBedroomZone)
        {
            _zoneService.UpdatePendingZoneAuthoringPreview();
            HandleZoneAuthoringInputs();
        }

        if (Input.GetKeyDown(NextCategoryKey))
        {
            CancelZoneAuthoringIfNeeded();
            _selectionService.SelectNextCategory();
            return;
        }

        if (Input.GetKeyDown(NextActionKey))
        {
            CancelZoneAuthoringIfNeeded();
            _selectionService.SelectNextAction();
            return;
        }

        selectedAction = _modeService.State.SelectedAction;
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
            _worldClockService.GetClockModeLabel());
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

    private void CancelZoneAuthoringIfNeeded()
    {
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
               actionType == RegistryActionType.DeleteDesignatedBed;
    }
}
