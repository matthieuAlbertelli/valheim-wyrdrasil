using UnityEngine;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Registry.UI;

namespace Wyrdrasil.Registry.Controllers;

public sealed class RegistryToolController
{
    private const KeyCode ToggleKey = KeyCode.F8;
    private const KeyCode NextCategoryKey = KeyCode.F9;
    private const KeyCode NextActionKey = KeyCode.F10;

    private readonly RegistryModeService _modeService;
    private readonly RegistrySelectionService _selectionService;
    private readonly RegistryActionRegistry _actionRegistry;
    private readonly RegistryContext _context;
    private readonly RegistryHudRenderer _hudRenderer;

    public RegistryToolController(
        RegistryModeService modeService,
        RegistrySelectionService selectionService,
        RegistryActionRegistry actionRegistry,
        RegistryContext context,
        RegistryHudRenderer hudRenderer)
    {
        _modeService = modeService;
        _selectionService = selectionService;
        _actionRegistry = actionRegistry;
        _context = context;
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

        if (Input.GetKeyDown(NextCategoryKey))
        {
            _selectionService.SelectNextCategory();
            return;
        }

        if (Input.GetKeyDown(NextActionKey))
        {
            _selectionService.SelectNextAction();
            return;
        }

        var selectedAction = _modeService.State.SelectedAction;
        var isDeleteAction = IsDeleteAction(selectedAction);

        if (!isDeleteAction && Input.GetMouseButtonDown(0))
        {
            _actionRegistry.Execute(selectedAction, _context);
            return;
        }

        if (isDeleteAction && Input.GetMouseButtonDown(1))
        {
            _actionRegistry.Execute(selectedAction, _context);
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
            _context.ZoneService.Zones.Count,
            _context.WaypointService.Waypoints.Count,
            _context.WaypointService.PendingLinkStartWaypointId,
            _context.SlotService.Slots.Count,
            _context.SeatService.Seats.Count,
            _context.ResidentService.RegisteredNpcs.Count);
    }

    private static bool IsDeleteAction(RegistryActionType actionType)
    {
        return actionType == RegistryActionType.DeleteZone ||
               actionType == RegistryActionType.DeleteSlot ||
               actionType == RegistryActionType.DeleteNavigationWaypoint ||
               actionType == RegistryActionType.DeleteDesignatedSeat;
    }
}
