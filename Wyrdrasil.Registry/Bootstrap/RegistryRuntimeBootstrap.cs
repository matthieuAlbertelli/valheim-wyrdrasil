using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Services.Interactions;
using Wyrdrasil.Registry.Services.Interactions.Modes;
using Wyrdrasil.Registry.UI;
using Wyrdrasil.Routines.Services;
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

    public static RegistryRuntimeBootstrap Create(
        ManualLogSource log,
        Wyrdrasil.Core.Services.RegistryModeService modeService,
        RegistrySettlementsBootstrap settlements,
        RegistryResidentsBootstrap residents,
        ConstructionModuleBootstrap constructionBootstrap,
        WorldClockService worldClockService,
        ResidentRoutineService residentRoutineService)
    {
        var diagnosticsService = new TargetDiagnosticsService(log);
        var craftStationAnchorEditorService = new CraftStationAnchorEditorService(log, settlements.CraftStationService);
        var deletionService = new RegistryDeletionService(
            log,
            settlements.BuildingService,
            settlements.ZoneService,
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            settlements.CraftStationService,
            settlements.WaypointService,
            residents.ResidentService,
            constructionBootstrap.TestingApi);

        var persistenceCoordinator = new WorldPersistenceCoordinator();
        var persistenceParticipants = new List<IWorldPersistenceParticipant>
        {
            new SettlementsPersistenceParticipant(
                log,
                settlements.BuildingService,
                settlements.ZoneService,
                settlements.WaypointService,
                settlements.SlotService,
                settlements.SeatService,
                settlements.BedService,
                settlements.CraftStationService),
            new RegistrySoulsPersistenceParticipant(residents.ResidentService),
            new RoutinesPersistenceParticipant(worldClockService),
            constructionBootstrap.PersistenceParticipant
        };

        var persistenceService = new RegistryPersistenceService(
            log,
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            settlements.CraftStationService,
            constructionBootstrap.RuntimeApi,
            residents.ResidentService,
            residentRoutineService,
            persistenceCoordinator,
            persistenceParticipants);

        var flushService = new RegistryFlushService(
            log,
            settlements.BuildingService,
            settlements.ZoneService,
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            settlements.CraftStationService,
            settlements.WaypointService,
            residents.ResidentService,
            persistenceService);

        var selectionService = new ToolSelectionService(modeService.State);
        var constructionDebugSessionService = new ConstructionDebugSessionService();
        var actionRegistry = RegistryActionRegistryFactory.CreateDefault(
            log,
            settlements.ZoneService,
            settlements.WaypointService,
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            settlements.CraftStationService,
            residents.SpawnService,
            residents.ResidentService,
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
            worldClockService);

        var constructionLinkVisualService = new ConstructionLinkVisualService(
            constructionBootstrap.ConstructionProjectService,
            constructionBootstrap.ConstructionProjectMarkerService,
            settlements.CraftStationService,
            residents.ResidentRuntimeService);

        var selectionFeedbackService = new RegistrySelectionFeedbackService(
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            settlements.CraftStationService,
            residents.ResidentService,
            constructionBootstrap.ConstructionProjectMarkerService,
            constructionDebugSessionService,
            constructionLinkVisualService);

        var runtimeFeedbackService = new RegistryRuntimeFeedbackService(
            settlements.ZoneService,
            constructionLinkVisualService,
            selectionFeedbackService);

        var constructionPreviewInteractionService = new RegistryConstructionPreviewInteractionService(
            log,
            settlements.ZoneService,
            constructionBootstrap.ConstructionPlacementPreviewService,
            constructionDebugSessionService);

        var zoneAuthoringInteractionService = new RegistryZoneAuthoringInteractionService(settlements.ZoneService);
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
            settlements.ZoneService,
            settlements.WaypointService,
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            residents.ResidentService,
            worldClockService,
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
