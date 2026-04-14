using BepInEx.Logging;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Persistence;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Construction.Bootstrap;

public sealed class ConstructionModuleBootstrap
{
    public ConstructionModuleBootstrap(
        ConstructionDebugStateService debugStateService,
        ConstructionDebugLogService debugLogService,
        BlueprintCatalogService blueprintCatalogService,
        ConstructionBlueprintCaptureService constructionBlueprintCaptureService,
        ConstructionPlacementService constructionPlacementService,
        ConstructionPlacementPreviewService constructionPlacementPreviewService,
        ConstructionOrderService constructionOrderService,
        ConstructionProjectService constructionProjectService,
        ConstructionGameTimeService constructionGameTimeService,
        ConstructionPieceBuildService constructionPieceBuildService,
        ConstructionProjectCleanupService constructionProjectCleanupService,
        ConstructionProjectProgressService constructionProjectProgressService,
        ConstructionPersistenceParticipant persistenceParticipant,
        IConstructionAuthoringApi authoringApi,
        IConstructionRuntimeApi runtimeApi,
        IConstructionTestingApi testingApi)
    {
        DebugStateService = debugStateService;
        DebugLogService = debugLogService;
        BlueprintCatalogService = blueprintCatalogService;
        ConstructionBlueprintCaptureService = constructionBlueprintCaptureService;
        ConstructionPlacementService = constructionPlacementService;
        ConstructionPlacementPreviewService = constructionPlacementPreviewService;
        ConstructionOrderService = constructionOrderService;
        ConstructionProjectService = constructionProjectService;
        ConstructionGameTimeService = constructionGameTimeService;
        ConstructionPieceBuildService = constructionPieceBuildService;
        ConstructionProjectCleanupService = constructionProjectCleanupService;
        ConstructionProjectProgressService = constructionProjectProgressService;
        PersistenceParticipant = persistenceParticipant;
        AuthoringApi = authoringApi;
        RuntimeApi = runtimeApi;
        TestingApi = testingApi;
    }

    public ConstructionDebugStateService DebugStateService { get; }
    public ConstructionDebugLogService DebugLogService { get; }
    public BlueprintCatalogService BlueprintCatalogService { get; }
    public ConstructionBlueprintCaptureService ConstructionBlueprintCaptureService { get; }
    public ConstructionPlacementService ConstructionPlacementService { get; }
    public ConstructionPlacementPreviewService ConstructionPlacementPreviewService { get; }
    public ConstructionOrderService ConstructionOrderService { get; }
    public ConstructionProjectService ConstructionProjectService { get; }
    public ConstructionGameTimeService ConstructionGameTimeService { get; }
    public ConstructionPieceBuildService ConstructionPieceBuildService { get; }
    public ConstructionProjectCleanupService ConstructionProjectCleanupService { get; }
    public ConstructionProjectProgressService ConstructionProjectProgressService { get; }
    public ConstructionPersistenceParticipant PersistenceParticipant { get; }
    public IConstructionAuthoringApi AuthoringApi { get; }
    public IConstructionRuntimeApi RuntimeApi { get; }
    public IConstructionTestingApi TestingApi { get; }

    public static ConstructionModuleBootstrap Create(ManualLogSource log, CraftStationService craftStationService)
    {
        var debugStateService = new ConstructionDebugStateService();
        var debugLogService = new ConstructionDebugLogService(log, debugStateService);
        var blueprintCatalogService = new BlueprintCatalogService();
        var constructionBlueprintCaptureService = new ConstructionBlueprintCaptureService();
        var constructionPlacementService = new ConstructionPlacementService(debugLogService);
        var constructionOrderService = new ConstructionOrderService();
        var constructionProjectService = new ConstructionProjectService(debugLogService, blueprintCatalogService, craftStationService);
        var constructionGameTimeService = new ConstructionGameTimeService();
        var constructionPieceBuildService = new ConstructionPieceBuildService(
            blueprintCatalogService,
            constructionPlacementService,
            constructionProjectService,
            debugLogService);
        var constructionProjectCleanupService = new ConstructionProjectCleanupService(
            constructionProjectService,
            debugLogService);
        var constructionProjectProgressService = new ConstructionProjectProgressService(
            constructionGameTimeService,
            constructionProjectService,
            constructionPieceBuildService,
            debugLogService);
        var constructionPlacementPreviewService = new ConstructionPlacementPreviewService(
            blueprintCatalogService,
            constructionPlacementService,
            constructionProjectService,
            debugLogService);
        var persistenceParticipant = new ConstructionPersistenceParticipant(blueprintCatalogService, constructionProjectService, debugLogService);
        var authoringApi = new ConstructionAuthoringApi(blueprintCatalogService, constructionProjectService, constructionBlueprintCaptureService, constructionOrderService, debugLogService);
        var runtimeApi = new ConstructionRuntimeApi(constructionProjectService, debugLogService);
        var testingApi = new ConstructionTestingApi(
            constructionProjectService,
            blueprintCatalogService,
            constructionPlacementService,
            constructionPieceBuildService,
            constructionProjectCleanupService,
            debugStateService,
            debugLogService);

        return new ConstructionModuleBootstrap(
            debugStateService,
            debugLogService,
            blueprintCatalogService,
            constructionBlueprintCaptureService,
            constructionPlacementService,
            constructionPlacementPreviewService,
            constructionOrderService,
            constructionProjectService,
            constructionGameTimeService,
            constructionPieceBuildService,
            constructionProjectCleanupService,
            constructionProjectProgressService,
            persistenceParticipant,
            authoringApi,
            runtimeApi,
            testingApi);
    }
}
