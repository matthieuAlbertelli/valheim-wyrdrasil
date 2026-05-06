using BepInEx.Logging;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Persistence;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Core.Services;

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
        ConstructionWorkPostGenerationService constructionWorkPostGenerationService,
        ConstructionPieceStabilityProbeService constructionPieceStabilityProbeService,
        ConstructionPreviewBuildPlanService constructionPreviewBuildPlanService,
        ConstructionPieceBuildOrderService constructionPieceBuildOrderService,
        ConstructionProjectService constructionProjectService,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionProjectGhostService constructionProjectGhostService,
        ConstructionProjectIntegrityService constructionProjectIntegrityService,
        ConstructionGameTimeService constructionGameTimeService,
        ConstructionPieceBuildService constructionPieceBuildService,
        ConstructionProjectProgressService constructionProjectProgressService,
        ConstructionProjectCleanupService constructionProjectCleanupService,
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
        ConstructionWorkPostGenerationService = constructionWorkPostGenerationService;
        ConstructionPieceStabilityProbeService = constructionPieceStabilityProbeService;
        ConstructionPreviewBuildPlanService = constructionPreviewBuildPlanService;
        ConstructionPieceBuildOrderService = constructionPieceBuildOrderService;
        ConstructionProjectService = constructionProjectService;
        ConstructionProjectMarkerService = constructionProjectMarkerService;
        ConstructionProjectGhostService = constructionProjectGhostService;
        ConstructionProjectIntegrityService = constructionProjectIntegrityService;
        ConstructionGameTimeService = constructionGameTimeService;
        ConstructionPieceBuildService = constructionPieceBuildService;
        ConstructionProjectProgressService = constructionProjectProgressService;
        ConstructionProjectCleanupService = constructionProjectCleanupService;
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
    public ConstructionWorkPostGenerationService ConstructionWorkPostGenerationService { get; }
    public ConstructionPieceStabilityProbeService ConstructionPieceStabilityProbeService { get; }
    public ConstructionPreviewBuildPlanService ConstructionPreviewBuildPlanService { get; }
    public ConstructionPieceBuildOrderService ConstructionPieceBuildOrderService { get; }
    public ConstructionProjectService ConstructionProjectService { get; }
    public ConstructionProjectMarkerService ConstructionProjectMarkerService { get; }
    public ConstructionProjectGhostService ConstructionProjectGhostService { get; }
    public ConstructionProjectIntegrityService ConstructionProjectIntegrityService { get; }
    public ConstructionGameTimeService ConstructionGameTimeService { get; }
    public ConstructionPieceBuildService ConstructionPieceBuildService { get; }
    public ConstructionProjectProgressService ConstructionProjectProgressService { get; }
    public ConstructionProjectCleanupService ConstructionProjectCleanupService { get; }
    public ConstructionPersistenceParticipant PersistenceParticipant { get; }
    public IConstructionAuthoringApi AuthoringApi { get; }
    public IConstructionRuntimeApi RuntimeApi { get; }
    public IConstructionTestingApi TestingApi { get; }

    public static ConstructionModuleBootstrap Create(ManualLogSource log, RegistryModeService registryModeService)
    {
        var debugStateService = new ConstructionDebugStateService();
        var debugLogService = new ConstructionDebugLogService(log, debugStateService);
        var blueprintCatalogService = new BlueprintCatalogService();
        var constructionBlueprintCaptureService = new ConstructionBlueprintCaptureService();
        var constructionPlacementService = new ConstructionPlacementService(debugLogService);
        var constructionOrderService = new ConstructionOrderService();
        var constructionWorkPostGenerationService = new ConstructionWorkPostGenerationService();
        var constructionProjectService = new ConstructionProjectService(debugLogService, constructionWorkPostGenerationService);
        var constructionPieceStabilityProbeService = new ConstructionPieceStabilityProbeService();
        var constructionPreviewBuildPlanService = new ConstructionPreviewBuildPlanService(constructionPieceStabilityProbeService);
        var constructionPieceBuildOrderService = new ConstructionPieceBuildOrderService(
            constructionPieceStabilityProbeService,
            constructionProjectService);
        var constructionProjectMarkerService = new ConstructionProjectMarkerService(constructionProjectService);
        var constructionProjectGhostService = new ConstructionProjectGhostService(
            registryModeService,
            blueprintCatalogService,
            constructionPlacementService,
            constructionPieceBuildOrderService,
            constructionProjectService);
        var constructionProjectIntegrityService = new ConstructionProjectIntegrityService(
            blueprintCatalogService,
            constructionProjectService,
            debugLogService);
        var constructionGameTimeService = new ConstructionGameTimeService();
        var constructionPieceBuildService = new ConstructionPieceBuildService(
            blueprintCatalogService,
            constructionPlacementService,
            constructionProjectService,
            constructionPieceBuildOrderService,
            debugLogService);
        var constructionProjectProgressService = new ConstructionProjectProgressService(
            constructionGameTimeService,
            constructionProjectService,
            constructionPieceBuildService,
            debugLogService);
        var constructionProjectCleanupService = new ConstructionProjectCleanupService(
            constructionProjectService,
            debugLogService);
        var constructionPlacementPreviewService = new ConstructionPlacementPreviewService(
            blueprintCatalogService,
            constructionPlacementService,
            constructionProjectService,
            constructionPreviewBuildPlanService,
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
            constructionWorkPostGenerationService,
            constructionPieceStabilityProbeService,
            constructionPreviewBuildPlanService,
            constructionPieceBuildOrderService,
            constructionProjectService,
            constructionProjectMarkerService,
            constructionProjectGhostService,
            constructionProjectIntegrityService,
            constructionGameTimeService,
            constructionPieceBuildService,
            constructionProjectProgressService,
            constructionProjectCleanupService,
            persistenceParticipant,
            authoringApi,
            runtimeApi,
            testingApi);
    }
}