using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.Occupations;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Registry.UI;
using Wyrdrasil.Routines;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.wyrdrasil.registry";
    public const string PluginName = "Wyrdrasil.Registry";
    public const string PluginVersion = "0.1.0";

    private RegistryToolController _registryToolController = null!;
    private RegistryPersistenceService _persistenceService = null!;
    private WorldClockService _worldClockService = null!;
    private ResidentRoutineService _residentRoutineService = null!;
    private TargetDiagnosticsService _diagnosticsService = null!;
    private ConstructionProjectProgressService _constructionProjectProgressService = null!;
    private Harmony? _harmony;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);

        RegistryModuleBootstrap.ApplyHarmony(_harmony);
        RoutinesModuleBootstrap.ApplyHarmony(_harmony);

        var modeService = new RegistryModeService(Logger);
        var buildingService = new BuildingService(Logger);
        var anchorPolicyService = new ZonePlacementPolicyService();
        var zoneService = new FunctionalZoneService(Logger, modeService, buildingService);
        var waypointService = new NavigationWaypointService(Logger, modeService, zoneService);
        var slotService = new ZoneSlotService(Logger, modeService, zoneService, anchorPolicyService);
        var seatService = new SeatService(Logger, modeService, zoneService, anchorPolicyService);
        var bedService = new BedService(Logger, modeService, zoneService, anchorPolicyService);
        var craftStationService = new CraftStationService(Logger, modeService, zoneService);
        var constructionBootstrap = ConstructionModuleBootstrap.Create(Logger, craftStationService);
        _constructionProjectProgressService = constructionBootstrap.ConstructionProjectProgressService;

        var appearanceCatalog = new NpcAppearanceCatalog();
        var equipmentCatalog = new NpcEquipmentCatalog();
        var appearanceGenerator = new NpcAppearanceGenerator(appearanceCatalog);
        var equipmentGenerator = new NpcEquipmentGenerator(equipmentCatalog);
        var identityGenerator = new NpcIdentityGenerator(appearanceGenerator, equipmentGenerator);
        var customizationApplier = new NpcCustomizationApplier(Logger);
        WyrdrasilVikingVisualBootstrap.ConfigureLogger(Logger);

        var vikingPrefabFactory = new VikingPrefabFactory(Logger);
        var spawnService = new NpcSpawnService(Logger, vikingPrefabFactory, identityGenerator, customizationApplier);

        var occupationNavigationStrategyRegistry = new OccupationNavigationStrategyRegistry();
        occupationNavigationStrategyRegistry.Register(new StandOccupationNavigationStrategy());
        occupationNavigationStrategyRegistry.Register(new SeatOccupationNavigationStrategy());
        occupationNavigationStrategyRegistry.Register(new BedOccupationNavigationStrategy());
        occupationNavigationStrategyRegistry.Register(new ApproachOccupationNavigationStrategy());

        var occupationLifecycleStrategyRegistry = new OccupationLifecycleStrategyRegistry();
        occupationLifecycleStrategyRegistry.Register(new StandOccupationLifecycleStrategy());
        occupationLifecycleStrategyRegistry.Register(new SeatOccupationLifecycleStrategy());
        occupationLifecycleStrategyRegistry.Register(new BedOccupationLifecycleStrategy());
        occupationLifecycleStrategyRegistry.Register(new AnchoredStandOccupationLifecycleStrategy());
        occupationLifecycleStrategyRegistry.Register(new CraftStationOccupationLifecycleStrategy());

        var occupationSustainStrategyRegistry = new OccupationSustainStrategyRegistry();
        occupationSustainStrategyRegistry.Register(new PassiveStandOccupationSustainStrategy());
        occupationSustainStrategyRegistry.Register(new PassiveSeatOccupationSustainStrategy());
        occupationSustainStrategyRegistry.Register(new PassiveBedOccupationSustainStrategy());
        occupationSustainStrategyRegistry.Register(new CraftStationOccupationSustainStrategy());
        occupationSustainStrategyRegistry.Register(new ConstructionWorkOccupationSustainStrategy(constructionBootstrap.RuntimeApi));

        var navigationService = new NpcNavigationService(Logger, occupationNavigationStrategyRegistry);
        var residentRuntimeService = new ResidentRuntimeService(Logger);
        var residentCatalogService = new ResidentCatalogService();
        var residentVisualService = new ResidentVisualService(modeService, residentRuntimeService);
        var scheduleService = new ResidentScheduleService();

        var anchorOccupationPlanBuilder = new AnchorOccupationPlanBuilder();

        var occupationTargetCatalog = new OccupationTargetCatalog();
        occupationTargetCatalog.Register(new SlotOccupationTargetSource(slotService));
        occupationTargetCatalog.Register(new SeatOccupationTargetSource(seatService));
        occupationTargetCatalog.Register(new BedOccupationTargetSource(bedService));
        occupationTargetCatalog.Register(new CraftStationOccupationTargetSource(craftStationService, anchorOccupationPlanBuilder));
        occupationTargetCatalog.Register(new ConstructionWorkPostOccupationTargetSource(constructionBootstrap.RuntimeApi, craftStationService, anchorOccupationPlanBuilder));

        var occupationClaimRegistry = new OccupationClaimRegistry();
        occupationClaimRegistry.Register(new PublicSeatOccupationClaimSource(seatService, occupationTargetCatalog));

        var occupationResolverRegistry = new OccupationResolverRegistry();
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            ResidentRoutineActivityType.WorkAtAssignedTarget,
            ResidentAssignmentPurpose.Work,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            ResidentRoutineActivityType.WorkAtAssignedSlot,
            ResidentAssignmentPurpose.Work,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            ResidentRoutineActivityType.WorkAtAssignedCraftStation,
            ResidentAssignmentPurpose.Work,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            ResidentRoutineActivityType.SitAtAssignedSeat,
            ResidentAssignmentPurpose.Meal,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            ResidentRoutineActivityType.SleepAtAssignedBed,
            ResidentAssignmentPurpose.Sleep,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new ClaimedOccupationResolver(
            ResidentRoutineActivityType.SitAtAvailablePublicSeat,
            occupationClaimRegistry));

        var occupationExecutionService = new OccupationExecutionService(
            residentRuntimeService,
            waypointService,
            navigationService,
            occupationLifecycleStrategyRegistry,
            occupationSustainStrategyRegistry);

        var occupationService = new ResidentOccupationService(
            residentRuntimeService,
            waypointService,
            navigationService,
            occupationExecutionService,
            occupationResolverRegistry);

        var residentPresenceService = new ResidentPresenceService(
            residentCatalogService,
            residentRuntimeService,
            spawnService,
            seatService,
            waypointService,
            occupationService,
            occupationResolverRegistry,
            residentVisualService);

        var residentAssignmentService = new ResidentAssignmentService(
            slotService,
            seatService,
            bedService,
            craftStationService,
            constructionBootstrap.RuntimeApi,
            residentRuntimeService,
            scheduleService,
            occupationService,
            residentCatalogService,
            residentVisualService);

        var residentService = new RegistryResidentService(
            Logger,
            modeService.State,
            slotService,
            seatService,
            bedService,
            craftStationService,
            residentRuntimeService,
            identityGenerator,
            customizationApplier,
            residentCatalogService,
            residentVisualService,
            residentPresenceService,
            residentAssignmentService,
            scheduleService);

        _worldClockService = new WorldClockService();
        _residentRoutineService = new ResidentRoutineService(
            Logger,
            _worldClockService,
            residentService,
            residentRuntimeService,
            occupationService);

        _diagnosticsService = new TargetDiagnosticsService(Logger);
        var craftStationAnchorEditorService = new CraftStationAnchorEditorService(Logger, craftStationService);
        var deletionService = new RegistryDeletionService(
            Logger,
            buildingService,
            zoneService,
            slotService,
            seatService,
            bedService,
            craftStationService,
            waypointService,
            residentService,
            constructionBootstrap.TestingApi);

        var persistenceCoordinator = new WorldPersistenceCoordinator();
        var persistenceParticipants = new List<IWorldPersistenceParticipant>
        {
            new SettlementsPersistenceParticipant(
                Logger,
                buildingService,
                zoneService,
                waypointService,
                slotService,
                seatService,
                bedService,
                craftStationService),

            new RegistrySoulsPersistenceParticipant(residentService),
            new RoutinesPersistenceParticipant(_worldClockService),
            constructionBootstrap.PersistenceParticipant
        };

        _persistenceService = new RegistryPersistenceService(
            Logger,
            slotService,
            seatService,
            bedService,
            craftStationService,
            constructionBootstrap.RuntimeApi,
            residentService,
            _residentRoutineService,
            persistenceCoordinator,
            persistenceParticipants);

        var flushService = new RegistryFlushService(
            Logger,
            buildingService,
            zoneService,
            slotService,
            seatService,
            bedService,
            craftStationService,
            waypointService,
            residentService,
            _persistenceService);

        var selectionService = new ToolSelectionService(modeService.State);
        var constructionDebugSessionService = new ConstructionDebugSessionService();
        var actionRegistry = BuildActionRegistry();

        var context = new RegistryContext(
            Logger,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            craftStationService,
            spawnService,
            residentService,
            _diagnosticsService,
            craftStationAnchorEditorService,
            deletionService,
            flushService,
            constructionBootstrap.AuthoringApi,
            constructionBootstrap.TestingApi,
            constructionBootstrap.ConstructionPlacementPreviewService,
            constructionDebugSessionService,
            _worldClockService);

        var hudRenderer = new RegistryHudRenderer();

        _registryToolController = new RegistryToolController(
            modeService,
            selectionService,
            actionRegistry,
            context,
            _persistenceService,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            craftStationService,
            residentService,
            _worldClockService,
            craftStationAnchorEditorService,
            hudRenderer);

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private static ActionRegistry BuildActionRegistry()
    {
        var registry = new ActionRegistry();
        registry.Register(new CreateTavernZoneAction());
        registry.Register(new CreateBedroomZoneAction());
        registry.Register(new CreateTavernWaypointAction());
        registry.Register(new ConnectNavigationWaypointsAction());
        registry.Register(new DeleteNavigationWaypointAction());
        registry.Register(new DeleteZoneAction());
        registry.Register(new CreateInnkeeperSlotAction());
        registry.Register(new DesignateSeatFurnitureAction());
        registry.Register(new DesignateBedFurnitureAction());
        registry.Register(new DesignateCraftStationFurnitureAction());
        registry.Register(new DeleteSlotAction());
        registry.Register(new DeleteDesignatedSeatAction());
        registry.Register(new DeleteDesignatedBedAction());
        registry.Register(new DeleteDesignatedCraftStationAction());
        registry.Register(new SpawnTestVikingAction());
        registry.Register(new RegisterNpcAction());
        registry.Register(new AssignInnkeeperRoleAction());
        registry.Register(new AssignSeatAction());
        registry.Register(new AssignBedAction());
        registry.Register(new ClearTargetInnkeeperSlotAssignmentAction());
        registry.Register(new ClearTargetSeatAssignmentAction());
        registry.Register(new ClearTargetBedAssignmentAction());
        registry.Register(new ForceAssignResidentAction());
        registry.Register(new DespawnTargetResidentAction());
        registry.Register(new RespawnAssignedResidentAction());
        registry.Register(new InspectTargetNpcAiAction());
        registry.Register(new EditTargetCraftStationAnchorAction());
        registry.Register(new ProbeAssignedCraftStationOccupationAction());
        registry.Register(new SimulateNoonAction());
        registry.Register(new SimulateNightAction());
        registry.Register(new ClearTimeSimulationAction());
        registry.Register(new CaptureBlueprintFromTargetZoneAction());
        registry.Register(new SpawnTestConstructionProjectAction());
        registry.Register(new DumpLatestConstructionProjectStateAction());
        registry.Register(new AssignTargetResidentToLatestConstructionProjectAction());
        registry.Register(new ClearTargetResidentConstructionAssignmentAction());
        registry.Register(new DeleteConstructionInTargetZoneAction());
        registry.Register(new ForceCompleteLatestConstructionProjectAction());
        registry.Register(new ResetLatestConstructionProjectAction());
        registry.Register(new ToggleConstructionVerboseLoggingAction());
        registry.Register(new PlaceBlueprintInstantlyAction());
        registry.Register(new FlushRegistryStateAction());
        registry.Register(new LoggingRegistryAction(RegistryActionType.None));
        return registry;
    }

    private void Update()
    {
        WyrdrasilPlayerCraftDebugMonitor.EnsureAttached(Player.m_localPlayer);
        _persistenceService.Update();
        _residentRoutineService.Update();
        _constructionProjectProgressService.Update();
        _registryToolController.Update();
    }

    private void OnGUI()
    {
        _registryToolController.OnGUI();
    }

    private void OnDestroy()
    {
        _persistenceService?.SaveWorldState();
        _harmony?.UnpatchSelf();
    }
}