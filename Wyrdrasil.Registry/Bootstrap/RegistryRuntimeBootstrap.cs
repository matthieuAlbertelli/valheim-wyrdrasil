using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.PlayerTool;
using Wyrdrasil.Registry.PlayerTool.Commanding;
using Wyrdrasil.Registry.PlayerTool.Localization;
using Wyrdrasil.Registry.PlayerTool.Recipes;
using Wyrdrasil.Registry.PlayerTool.Visual;
using Wyrdrasil.Registry.PlayerTool.Assignments;
using Wyrdrasil.Registry.PlayerTool.Marking;
using Wyrdrasil.Registry.PlayerTool.Inspection;
using Wyrdrasil.Registry.PlayerTool.WorldObjects;
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
        RegistryCraftStationIntegrityService craftStationIntegrityService,
        RegistrySelectionFeedbackService selectionFeedbackService,
        RegistryRuntimeFeedbackService runtimeFeedbackService,
        RegistryConstructionPreviewInteractionService constructionPreviewInteractionService,
        RegistryZoneAuthoringInteractionService zoneAuthoringInteractionService,
        RegistryInteractionSessionCoordinator interactionSessionCoordinator,
        RegistryInteractionModeRouter interactionModeRouter,
        RegistryPlayerToolItemService registryPlayerToolItemService,
        RegistryPlayerToolRuntimeService registryPlayerToolRuntimeService,
        RegistryPlayerToolCommandMessageService registryPlayerToolCommandMessageService,
        RegistryToolController registryToolController)
    {
        DiagnosticsService = diagnosticsService;
        CraftStationAnchorEditorService = craftStationAnchorEditorService;
        DeletionService = deletionService;
        PersistenceService = persistenceService;
        FlushService = flushService;
        ConstructionDebugSessionService = constructionDebugSessionService;
        ConstructionLinkVisualService = constructionLinkVisualService;
        CraftStationIntegrityService = craftStationIntegrityService;
        SelectionFeedbackService = selectionFeedbackService;
        RuntimeFeedbackService = runtimeFeedbackService;
        ConstructionPreviewInteractionService = constructionPreviewInteractionService;
        ZoneAuthoringInteractionService = zoneAuthoringInteractionService;
        InteractionSessionCoordinator = interactionSessionCoordinator;
        InteractionModeRouter = interactionModeRouter;
        RegistryPlayerToolItemService = registryPlayerToolItemService;
        RegistryPlayerToolRuntimeService = registryPlayerToolRuntimeService;
        RegistryPlayerToolCommandMessageService = registryPlayerToolCommandMessageService;
        RegistryToolController = registryToolController;
    }

    public TargetDiagnosticsService DiagnosticsService { get; }
    public CraftStationAnchorEditorService CraftStationAnchorEditorService { get; }
    public RegistryDeletionService DeletionService { get; }
    public RegistryPersistenceService PersistenceService { get; }
    public RegistryFlushService FlushService { get; }
    public ConstructionDebugSessionService ConstructionDebugSessionService { get; }
    public ConstructionLinkVisualService ConstructionLinkVisualService { get; }
    public RegistryCraftStationIntegrityService CraftStationIntegrityService { get; }
    public RegistrySelectionFeedbackService SelectionFeedbackService { get; }
    public RegistryRuntimeFeedbackService RuntimeFeedbackService { get; }
    public RegistryConstructionPreviewInteractionService ConstructionPreviewInteractionService { get; }
    public RegistryZoneAuthoringInteractionService ZoneAuthoringInteractionService { get; }
    public RegistryInteractionSessionCoordinator InteractionSessionCoordinator { get; }
    public RegistryInteractionModeRouter InteractionModeRouter { get; }
    public RegistryPlayerToolItemService RegistryPlayerToolItemService { get; }
    public RegistryPlayerToolRuntimeService RegistryPlayerToolRuntimeService { get; }
    public RegistryPlayerToolCommandMessageService RegistryPlayerToolCommandMessageService { get; }
    public RegistryToolController RegistryToolController { get; }

    internal static RegistryRuntimeBootstrap Create(
        ManualLogSource log,
        string pluginLocation,
        RegistryPlayerToolVisualConfig visualConfig,
        Wyrdrasil.Core.Services.RegistryModeService modeService,
        RegistrySettlementsBootstrap settlements,
        RegistryResidentsBootstrap residents,
        ConstructionModuleBootstrap constructionBootstrap)
    {
        var diagnosticsService = new TargetDiagnosticsService(log);
        var craftStationAnchorEditorService = new CraftStationAnchorEditorService(log, settlements.Services.CraftStationService);
        var deletionService = new RegistryDeletionService(
            log,
            settlements.AuthoringApi,
            settlements.RuntimeApi,
            settlements.Services.SeatService,
            settlements.Services.BedService,
            settlements.Services.CraftStationService,
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
            settlements.RuntimeApi,
            residents.Services.ResidentService,
            persistenceService);

        var craftStationIntegrityService = new RegistryCraftStationIntegrityService(
            log,
            settlements.Services.CraftStationService,
            settlements.PersistenceParticipant,
            residents.Services.ResidentService,
            persistenceService);

        var selectionService = new ToolSelectionService(modeService.State);
        var constructionDebugSessionService = new ConstructionDebugSessionService();
        var registryPlayerToolVisualBundleService = new RegistryPlayerToolVisualBundleService(
            log,
            pluginLocation,
            visualConfig);
        var registryPlayerToolRecipeService = new RegistryPlayerToolRecipeService(log);
        var registryPlayerToolLocalizationService = new RegistryPlayerToolLocalizationService(log);
        var registryPlayerToolCommandMessageService = new RegistryPlayerToolCommandMessageService();

        var registryPlayerToolBlueprintThumbnailService = new RegistryPlayerToolBlueprintThumbnailService(
            log,
            constructionBootstrap.AuthoringApi);
        var registryPlayerToolPieceTableService = new RegistryPlayerToolPieceTableService(
            log,
            constructionBootstrap.AuthoringApi,
            registryPlayerToolBlueprintThumbnailService);
        var registryPlayerToolInspectionService = new RegistryPlayerToolInspectionService(log);
        var registryPlayerToolSaveService = new RegistryPlayerToolSaveService(log, persistenceService);
        var constructionPreviewInteractionService = new RegistryConstructionPreviewInteractionService(
            log,
            settlements.AuthoringApi,
            constructionBootstrap.ConstructionPlacementPreviewService,
            constructionDebugSessionService);
        var registryPlayerToolWorldObjectHandlers = new IRegistryPlayerToolWorldObjectTargetHandler[]
        {
            new RegistryPlayerToolBedWorldObjectTargetHandler(
                settlements.AuthoringApi,
                deletionService),
            new RegistryPlayerToolCraftStationWorldObjectTargetHandler(
                settlements.AuthoringApi,
                deletionService)
        };
        var registryPlayerToolMarkingService = new RegistryPlayerToolMarkingService(
            log,
            registryPlayerToolPieceTableService,
            registryPlayerToolWorldObjectHandlers);
        var constructionLinkVisualService = new ConstructionLinkVisualService(
            constructionBootstrap.ConstructionProjectService,
            constructionBootstrap.ConstructionProjectMarkerService,
            settlements.RuntimeApi,
            residents.RuntimeApi);

        var registryPlayerToolAssignmentService = new RegistryPlayerToolAssignmentService(
            log,
            residents.Services.ResidentService,
            settlements.Services.CraftStationService,
            constructionBootstrap.RuntimeApi,
            constructionBootstrap.ConstructionProjectMarkerService,
            constructionDebugSessionService,
            constructionLinkVisualService,
            registryPlayerToolPieceTableService,
            new IRegistryPlayerToolAssignmentTargetHandler[]
            {
                new RegistryPlayerToolBedAssignmentTargetHandler(
                    registryPlayerToolWorldObjectHandlers[0],
                    residents.Services.ResidentAssignmentService),
                new RegistryPlayerToolCraftStationAssignmentTargetHandler(
                    registryPlayerToolWorldObjectHandlers[1],
                    residents.Services.ResidentAssignmentService)
            });
        var registryPlayerToolGameplayActionService = new RegistryPlayerToolGameplayActionService(
            log,
            settlements.AuthoringApi,
            residents.Services.ResidentService,
            registryPlayerToolMarkingService,
            registryPlayerToolAssignmentService,
            constructionBootstrap.AuthoringApi,
            constructionDebugSessionService,
            constructionPreviewInteractionService);
        RegistryPlayerToolActionRouter.Configure(
            log,
            registryPlayerToolInspectionService,
            registryPlayerToolGameplayActionService,
            registryPlayerToolSaveService,
            constructionPreviewInteractionService);
        var registryPlayerToolTargetFeedbackService = new RegistryPlayerToolTargetFeedbackService(
            residents.Services.ResidentService);
        var registryPlayerToolInspectionLinkService = new RegistryPlayerToolInspectionLinkService(
            residents.RuntimeApi,
            settlements.RuntimeApi,
            constructionBootstrap.RuntimeApi);
        var registryPlayerToolInspectionRevealService = new RegistryPlayerToolInspectionRevealService(
            constructionBootstrap.ConstructionProjectMarkerService,
            constructionBootstrap.ConstructionProjectGhostService,
            registryPlayerToolInspectionLinkService,
            settlements.RuntimeApi,
            residents.RuntimeApi);
        var registryPlayerToolRuntimeService = new RegistryPlayerToolRuntimeService(
            settlements.AuthoringApi,
            registryPlayerToolGameplayActionService,
            registryPlayerToolTargetFeedbackService,
            registryPlayerToolInspectionRevealService,
            constructionPreviewInteractionService,
            registryPlayerToolSaveService);
        var registryPlayerToolItemService = new RegistryPlayerToolItemService(
            log,
            registryPlayerToolPieceTableService,
            registryPlayerToolVisualBundleService,
            registryPlayerToolRecipeService,
            registryPlayerToolLocalizationService);
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
            craftStationIntegrityService,
            selectionFeedbackService,
            runtimeFeedbackService,
            constructionPreviewInteractionService,
            zoneAuthoringInteractionService,
            interactionSessionCoordinator,
            interactionModeRouter,
            registryPlayerToolItemService,
            registryPlayerToolRuntimeService,
            registryPlayerToolCommandMessageService,
            registryToolController);
    }
}
