using UnityEngine;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Registry.UI;

namespace Wyrdrasil.Registry.Controllers;

public sealed class RegistryToolController
{
    private const KeyCode ToggleKey = KeyCode.F8;

    private readonly RegistryModeService _modeService;
    private readonly RegistryPersistenceService _persistenceService;
    private readonly RegistryInteractionSessionCoordinator _interactionSessionCoordinator;
    private readonly RegistryInteractionModeRouter _interactionModeRouter;
    private readonly RegistryRuntimeFeedbackService _runtimeFeedbackService;
    private readonly RegistryHudStateProvider _hudStateProvider;
    private readonly RegistryHudRenderer _hudRenderer;

    public RegistryToolController(
        RegistryModeService modeService,
        RegistryPersistenceService persistenceService,
        RegistryInteractionSessionCoordinator interactionSessionCoordinator,
        RegistryInteractionModeRouter interactionModeRouter,
        RegistryRuntimeFeedbackService runtimeFeedbackService,
        RegistryHudStateProvider hudStateProvider,
        RegistryHudRenderer hudRenderer)
    {
        _modeService = modeService;
        _persistenceService = persistenceService;
        _interactionSessionCoordinator = interactionSessionCoordinator;
        _interactionModeRouter = interactionModeRouter;
        _runtimeFeedbackService = runtimeFeedbackService;
        _hudStateProvider = hudStateProvider;
        _hudRenderer = hudRenderer;
    }

    public void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            ExitRegistryMode();
            return;
        }

        if (!_modeService.IsRegistryModeEnabled)
        {
            _runtimeFeedbackService.ClearAll();
            return;
        }

        var state = _modeService.State;
        _runtimeFeedbackService.Update(state);

        _interactionModeRouter.Update(state, out var shouldSave);
        if (shouldSave)
        {
            _persistenceService.SaveWorldState();
        }
    }

    public void OnGUI()
    {
        if (!_modeService.IsRegistryModeEnabled)
        {
            return;
        }

        _hudRenderer.Draw(_hudStateProvider.Create(_modeService.State));
    }

    private void ExitRegistryMode()
    {
        _interactionSessionCoordinator.CancelInteractiveAuthoring();
        _modeService.ToggleRegistryMode();
        _runtimeFeedbackService.ClearAll();
    }
}
