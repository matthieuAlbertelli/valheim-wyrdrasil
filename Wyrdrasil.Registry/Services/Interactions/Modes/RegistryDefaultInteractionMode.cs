using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.Services.Interactions.Modes;

public sealed class RegistryDefaultInteractionMode : IRegistryToolInteractionMode
{
    private const KeyCode NextCategoryKey = KeyCode.F9;
    private const KeyCode NextActionKey = KeyCode.F10;

    private readonly ToolSelectionService _selectionService;
    private readonly ActionRegistry _actionRegistry;
    private readonly RegistryInteractionSessionCoordinator _interactionSessionCoordinator;

    public RegistryDefaultInteractionMode(
        ToolSelectionService selectionService,
        ActionRegistry actionRegistry,
        RegistryInteractionSessionCoordinator interactionSessionCoordinator)
    {
        _selectionService = selectionService;
        _actionRegistry = actionRegistry;
        _interactionSessionCoordinator = interactionSessionCoordinator;
    }

    public string Name => "Default";

    public bool CanHandle(RegistryToolState state)
    {
        return true;
    }

    public void Update(RegistryToolState state, out bool shouldSave)
    {
        shouldSave = false;

        if (HandleSelectionCyclingInputs())
        {
            return;
        }

        shouldSave = HandleActionExecution(state.SelectedAction);
    }

    private bool HandleSelectionCyclingInputs()
    {
        if (Input.GetKeyDown(NextCategoryKey))
        {
            _interactionSessionCoordinator.CancelInteractiveAuthoring();
            _selectionService.SelectNextCategory();
            return true;
        }

        if (!Input.GetKeyDown(NextActionKey))
        {
            return false;
        }

        _interactionSessionCoordinator.CancelInteractiveAuthoring();
        _selectionService.SelectNextAction();
        return true;
    }

    private bool HandleActionExecution(RegistryActionType selectedAction)
    {
        var isDeleteAction = IsDeleteAction(selectedAction);

        if (!isDeleteAction && Input.GetMouseButtonDown(0))
        {
            _actionRegistry.Execute(selectedAction);
            return true;
        }

        if (isDeleteAction && Input.GetMouseButtonDown(1))
        {
            _actionRegistry.Execute(selectedAction);
            return true;
        }

        return false;
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
