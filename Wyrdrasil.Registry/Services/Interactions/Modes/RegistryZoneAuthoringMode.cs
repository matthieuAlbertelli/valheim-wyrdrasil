using UnityEngine;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry.Services.Interactions.Modes;

public sealed class RegistryZoneAuthoringMode : IRegistryToolInteractionMode
{
    private const KeyCode NextCategoryKey = KeyCode.F9;
    private const KeyCode NextActionKey = KeyCode.F10;

    private readonly ToolSelectionService _selectionService;
    private readonly ActionRegistry _actionRegistry;
    private readonly RegistryZoneAuthoringInteractionService _zoneAuthoringInteractionService;
    private readonly RegistryInteractionSessionCoordinator _interactionSessionCoordinator;

    public RegistryZoneAuthoringMode(
        ToolSelectionService selectionService,
        ActionRegistry actionRegistry,
        RegistryZoneAuthoringInteractionService zoneAuthoringInteractionService,
        RegistryInteractionSessionCoordinator interactionSessionCoordinator)
    {
        _selectionService = selectionService;
        _actionRegistry = actionRegistry;
        _zoneAuthoringInteractionService = zoneAuthoringInteractionService;
        _interactionSessionCoordinator = interactionSessionCoordinator;
    }

    public string Name => "ZoneAuthoring";

    public bool CanHandle(RegistryToolState state)
    {
        return _zoneAuthoringInteractionService.ShouldHandle(state.SelectedAction);
    }

    public void Update(RegistryToolState state, out bool shouldSave)
    {
        shouldSave = false;
        _zoneAuthoringInteractionService.UpdatePreviewIfNeeded(state.SelectedAction);

        if (HandleSelectionCyclingInputs())
        {
            return;
        }

        if (_zoneAuthoringInteractionService.TryHandleSecondaryInput())
        {
            return;
        }

        if (_zoneAuthoringInteractionService.TryHandleHeightAdjustment())
        {
            return;
        }

        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        _actionRegistry.Execute(state.SelectedAction);
        shouldSave = true;
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
}
