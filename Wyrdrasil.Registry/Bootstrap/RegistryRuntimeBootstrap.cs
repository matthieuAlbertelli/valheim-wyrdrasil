using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Registry.Services.Interactions.Modes;
using Wyrdrasil.Registry.UI;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Bootstrap;

public sealed class RegistryRuntimeBootstrap
{
    public RegistryRuntimeBootstrap(
        TargetDiagnosticsService diagnosticsService,
        CraftStationAnchorEditorService craftStationAnchorEditorService,
        RegistryDeletionService deletionService,
        RegistryPersistenceService persistenceService,
        RegistryFlushService flushService,
        ConstructionDebugSessionService constructionDebugSessionService,
        ConstructionLinkVisualService constructionLinkVisualService,
        RegistrySelectionFeedbackService selectionFeedbackService,
        RegistryRuntimeFeedbackService runtimeFeedbackService,
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService,
        RegistryZoneAuthoringInteractionService zoneAuthoringInteractionService,
        RegistryInteractionSessionCoordinator interactionSessionCoordinator,
        RegistryInteractionModeRouter interactionModeRouter,
        RegistryToolController registryToolController)
    {
        DiagnosticsService = diagnosticsService;
        CraftStationAnchorEditorService = craftStationAnchorEditorService;
        DeletionService = deletionService;
        PersistenceService = persistenceService;
        FlushService = flushService;
        ConstructionDebugSessionService = constructionDebugSessionService;
        ConstructionLinkVisualService = constructionLinkVisualService;
        SelectionFeedbackService = selectionFeedbackService;
        RuntimeFeedbackService = runtimeFeedbackService;
        ConstructionPreviewInteractionService = constructionPreviewInteractionService;
        ZoneAuthoringInteractionService = zoneAuthoringInteractionService;
        InteractionSessionCoordinator = interactionSessionCoordinator;
        InteractionModeRouter = interactionModeRouter;
        RegistryToolController = registryToolController;
    }

    public TargetDiagnosticsService DiagnosticsService { get; }
    public CraftStationAnchorEditorService CraftStationAnchorEditorService { get; }
    public RegistryDeletionService DeletionService { get; }
    public RegistryPersistenceService PersistenceService { get; }
    public RegistryFlushService FlushService { get; }
    public ConstructionDebugSessionService ConstructionDebugSessionService { get; }
    public ConstructionLinkVisualService ConstructionLinkVisualService { get; }
    public RegistrySelectionFeedbackService SelectionFeedbackService { get; }
    public RegistryRuntimeFeedbackService RuntimeFeedbackService { get; }
    public RegistryConstructionPreviewInteractionService ConstructionPreviewInteractionService { get; }
    public RegistryZoneAuthoringInteractionService ZoneAuthoringInteractionService { get; }
    public RegistryInteractionSessionCoordinator InteractionSessionCoordinator { get; }
    public RegistryInteractionModeRouter InteractionModeRouter { get; }
    public RegistryToolController RegistryToolController { get; }

    internal static RegistryRuntimeBootstrap Create(
        ManualLogSource log,
        Wyrdrasil.Core.Services.RegistryModeService modeService,
        RegistrySettlementsBootstrap settlements,
        RegistryResidentsBootstrap residents,
        ConstructionModuleBootstrap constructionBootstrap)
    {
        var diagnosticsService = new TargetDiagnosticsService(log);
        var craftStationAnchorEditorService = new CraftStationAnchorEditorService(log, settlements.Services.CraftStationService);
        var deletionService = new RegistryDeletionService(
            log,
            settlements.Services.BuildingService,
            settlements.Services.ZoneService,
            settlements.Services.SlotService,
            settlements.Services.SeatService,
            settlements.Services.BedService,
            settlements.Services.CraftStationService,
            settlements.Services.WaypointService,
            residents.Services.ResidentService,
            constructionBootstrap.TestingApi);

        var persistenceCoordinator = new WorldPersistenceCoordinator();
        var persistenceParticipants = new List<IWorldPersistenceParticipant>
        {
            settlements.PersistenceParticipant,
            residents.SoulsPersistenceParticipant,
            residents.RoutinesPersistenceParticipant,
            constructionBootstrap.PersistenceParticipant
        };

        var persistenceRestoreHooks = new List<IWorldPersistenceRestoreHook>
        {
            residents.CreatePersistenceRestoreHook(settlements.RuntimeApi, constructionBootstrap.RuntimeApi)
        };

        var persistenceService = new RegistryPersistenceService(
            log,
            persistenceCoordinator,
            persistenceParticipants,
            persistenceRestoreHooks);

        var flushService = new RegistryFlushService(
            log,
            settlements.Services.BuildingService,
            settlements.Services.ZoneService,
            settlements.Services.SlotService,
            settlements.Services.SeatService,
            settlements.Services.BedService,
            settlements.Services.CraftStationService,
            settlements.Services.WaypointService,
            residents.Services.ResidentService,
            persistenceService);

        var selectionService = new ToolSelectionService(modeService.State);
        var constructionDebugSessionService = new ConstructionDebugSessionService();
        var actionRegistry = RegistryActionRegistryFactory.CreateDefault(
            log,
            settlements.AuthoringApi,
            settlements.Services.WaypointService,
            settlements.Services.SlotService,
            settlements.Services.SeatService,
            settlements.Services.BedService,
            settlements.AuthoringApi,
            residents.AuthoringApi,
            residents.Services.ResidentService,
            diagnosticsService,
            craftStationAnchorEditorService,
            deletionService,
            flushService,
            constructionBootstrap.AuthoringApi,
            constructionBootstrap.TestingApi,
            constructionBootstrap.RuntimeApi,
            constructionBootstrap.ConstructionProjectMarkerService,
            constructionBootstrap.ConstructionPlacementPreviewService,
            constructionDebugSessionService,
            residents.RoutinesRuntimeApi);

        var constructionLinkVisualService = new ConstructionLinkVisualService(
            constructionBootstrap.ConstructionProjectService,
            constructionBootstrap.ConstructionProjectMarkerService,
            settlements.RuntimeApi,
            residents.RuntimeApi);

        var selectionFeedbackService = new RegistrySelectionFeedbackService(
            settlements.Services.SlotService,
            settlements.Services.SeatService,
            settlements.Services.BedService,
            settlements.Services.CraftStationService,
            residents.Services.ResidentService,
            constructionBootstrap.ConstructionProjectMarkerService,
            constructionDebugSessionService,
            constructionLinkVisualService);

        var runtimeFeedbackService = new RegistryRuntimeFeedbackService(
            settlements.AuthoringApi,
            constructionLinkVisualService,
            selectionFeedbackService);

        var constructionPreviewInteractionService = new RegistryConstructionPreviewInteractionService(
            log,
            settlements.AuthoringApi,
            constructionBootstrap.ConstructionPlacementPreviewService,
            constructionDebugSessionService);

        var zoneAuthoringInteractionService = new RegistryZoneAuthoringInteractionService(settlements.AuthoringApi);
        var interactionSessionCoordinator = new RegistryInteractionSessionCoordinator(
            constructionPreviewInteractionService,
            zoneAuthoringInteractionService,
            craftStationAnchorEditorService);
        var interactionModeRouter = new RegistryInteractionModeRouter(
            new IRegistryToolInteractionMode[]
            {
                new RegistryConstructionPreviewMode(constructionPreviewInteractionService),
                new RegistryCraftStationAnchorEditorMode(craftStationAnchorEditorService),
                new RegistryZoneAuthoringMode(
                    selectionService,
                    actionRegistry,
                    zoneAuthoringInteractionService,
                    interactionSessionCoordinator)
            },
            new RegistryDefaultInteractionMode(
                selectionService,
                actionRegistry,
                interactionSessionCoordinator));

        var hudStateProvider = new RegistryHudStateProvider(
            settlements.RuntimeApi,
            residents.Services.ResidentService,
            residents.RoutinesRuntimeApi,
            craftStationAnchorEditorService,
            constructionPreviewInteractionService,
            zoneAuthoringInteractionService,
            interactionModeRouter);

        var registryToolController = new RegistryToolController(
            modeService,
            persistenceService,
            interactionSessionCoordinator,
            interactionModeRouter,
            runtimeFeedbackService,
            hudStateProvider,
            new RegistryHudRenderer());

        return new RegistryRuntimeBootstrap(
            diagnosticsService,
            craftStationAnchorEditorService,
            deletionService,
            persistenceService,
            flushService,
            constructionDebugSessionService,
            constructionLinkVisualService,
            selectionFeedbackService,
            runtimeFeedbackService,
            constructionPreviewInteractionService,
            zoneAuthoringInteractionService,
            interactionSessionCoordinator,
            interactionModeRouter,
            registryToolController);
    }
}
