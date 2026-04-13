using BepInEx.Logging;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Persistence;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Construction.Testing;

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
    public ConstructionPersistenceParticipant PersistenceParticipant { get; }
    public IConstructionAuthoringApi AuthoringApi { get; }
    public IConstructionRuntimeApi RuntimeApi { get; }
    public IConstructionTestingApi TestingApi { get; }

    public static ConstructionModuleBootstrap Create(ManualLogSource log)
    {
        var debugStateService = new ConstructionDebugStateService();
        var debugLogService = new ConstructionDebugLogService(log, debugStateService);
        var blueprintCatalogService = new BlueprintCatalogService();
        var constructionBlueprintCaptureService = new ConstructionBlueprintCaptureService();
        var constructionPlacementService = new ConstructionPlacementService(debugLogService);
        var constructionPlacementPreviewService = new ConstructionPlacementPreviewService(
            blueprintCatalogService,
            constructionPlacementService,
            debugLogService);
        var constructionOrderService = new ConstructionOrderService();
        var constructionProjectService = new ConstructionProjectService(debugLogService);
        var persistenceParticipant = new ConstructionPersistenceParticipant(blueprintCatalogService, constructionProjectService, debugLogService);
        var authoringApi = new ConstructionAuthoringApi(blueprintCatalogService, constructionProjectService, constructionBlueprintCaptureService, constructionOrderService, debugLogService);
        var runtimeApi = new ConstructionRuntimeApi(constructionProjectService, debugLogService);
        var testingApi = new ConstructionTestingApi(constructionProjectService, blueprintCatalogService, constructionPlacementService, debugStateService, debugLogService);

        return new ConstructionModuleBootstrap(
            debugStateService,
            debugLogService,
            blueprintCatalogService,
            constructionBlueprintCaptureService,
            constructionPlacementService,
            constructionPlacementPreviewService,
            constructionOrderService,
            constructionProjectService,
            persistenceParticipant,
            authoringApi,
            runtimeApi,
            testingApi);
    }
}
