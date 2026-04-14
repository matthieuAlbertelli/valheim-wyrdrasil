using BepInEx.Logging;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Registry.Occupations;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Bootstrap;

public sealed class RegistryResidentsBootstrap
{
    public RegistryResidentsBootstrap(
        NpcIdentityGenerator identityGenerator,
        NpcCustomizationApplier customizationApplier,
        NpcSpawnService spawnService,
        OccupationNavigationStrategyRegistry occupationNavigationStrategyRegistry,
        OccupationLifecycleStrategyRegistry occupationLifecycleStrategyRegistry,
        OccupationSustainStrategyRegistry occupationSustainStrategyRegistry,
        NpcNavigationService navigationService,
        ResidentRuntimeService residentRuntimeService,
        ResidentCatalogService residentCatalogService,
        ResidentVisualService residentVisualService,
        ResidentScheduleService scheduleService,
        AnchorOccupationPlanBuilder anchorOccupationPlanBuilder,
        OccupationTargetCatalog occupationTargetCatalog,
        OccupationClaimRegistry occupationClaimRegistry,
        OccupationResolverRegistry occupationResolverRegistry,
        OccupationExecutionService occupationExecutionService,
        ResidentOccupationService occupationService,
        ResidentPresenceService residentPresenceService,
        ResidentAssignmentService residentAssignmentService,
        RegistryResidentService residentService)
    {
        IdentityGenerator = identityGenerator;
        CustomizationApplier = customizationApplier;
        SpawnService = spawnService;
        OccupationNavigationStrategyRegistry = occupationNavigationStrategyRegistry;
        OccupationLifecycleStrategyRegistry = occupationLifecycleStrategyRegistry;
        OccupationSustainStrategyRegistry = occupationSustainStrategyRegistry;
        NavigationService = navigationService;
        ResidentRuntimeService = residentRuntimeService;
        ResidentCatalogService = residentCatalogService;
        ResidentVisualService = residentVisualService;
        ScheduleService = scheduleService;
        AnchorOccupationPlanBuilder = anchorOccupationPlanBuilder;
        OccupationTargetCatalog = occupationTargetCatalog;
        OccupationClaimRegistry = occupationClaimRegistry;
        OccupationResolverRegistry = occupationResolverRegistry;
        OccupationExecutionService = occupationExecutionService;
        OccupationService = occupationService;
        ResidentPresenceService = residentPresenceService;
        ResidentAssignmentService = residentAssignmentService;
        ResidentService = residentService;
    }

    public NpcIdentityGenerator IdentityGenerator { get; }
    public NpcCustomizationApplier CustomizationApplier { get; }
    public NpcSpawnService SpawnService { get; }
    public OccupationNavigationStrategyRegistry OccupationNavigationStrategyRegistry { get; }
    public OccupationLifecycleStrategyRegistry OccupationLifecycleStrategyRegistry { get; }
    public OccupationSustainStrategyRegistry OccupationSustainStrategyRegistry { get; }
    public NpcNavigationService NavigationService { get; }
    public ResidentRuntimeService ResidentRuntimeService { get; }
    public ResidentCatalogService ResidentCatalogService { get; }
    public ResidentVisualService ResidentVisualService { get; }
    public ResidentScheduleService ScheduleService { get; }
    public AnchorOccupationPlanBuilder AnchorOccupationPlanBuilder { get; }
    public OccupationTargetCatalog OccupationTargetCatalog { get; }
    public OccupationClaimRegistry OccupationClaimRegistry { get; }
    public OccupationResolverRegistry OccupationResolverRegistry { get; }
    public OccupationExecutionService OccupationExecutionService { get; }
    public ResidentOccupationService OccupationService { get; }
    public ResidentPresenceService ResidentPresenceService { get; }
    public ResidentAssignmentService ResidentAssignmentService { get; }
    public RegistryResidentService ResidentService { get; }

    public static RegistryResidentsBootstrap Create(
        ManualLogSource log,
        RegistrySettlementsBootstrap settlements,
        ConstructionModuleBootstrap constructionBootstrap,
        Wyrdrasil.Core.Services.RegistryModeService modeService)
    {
        var appearanceCatalog = new NpcAppearanceCatalog();
        var equipmentCatalog = new NpcEquipmentCatalog();
        var appearanceGenerator = new NpcAppearanceGenerator(appearanceCatalog);
        var equipmentGenerator = new NpcEquipmentGenerator(equipmentCatalog);
        var identityGenerator = new NpcIdentityGenerator(appearanceGenerator, equipmentGenerator);
        var customizationApplier = new NpcCustomizationApplier(log);
        WyrdrasilVikingVisualBootstrap.ConfigureLogger(log);

        var vikingPrefabFactory = new VikingPrefabFactory(log);
        var spawnService = new NpcSpawnService(log, vikingPrefabFactory, identityGenerator, customizationApplier);

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

        var navigationService = new NpcNavigationService(log, occupationNavigationStrategyRegistry);
        var residentRuntimeService = new ResidentRuntimeService(log);
        var residentCatalogService = new ResidentCatalogService();
        var residentVisualService = new ResidentVisualService(modeService, residentRuntimeService);
        var scheduleService = new ResidentScheduleService();
        var anchorOccupationPlanBuilder = new AnchorOccupationPlanBuilder();

        var occupationTargetCatalog = new OccupationTargetCatalog();
        occupationTargetCatalog.Register(new SlotOccupationTargetSource(settlements.SlotService));
        occupationTargetCatalog.Register(new SeatOccupationTargetSource(settlements.SeatService));
        occupationTargetCatalog.Register(new BedOccupationTargetSource(settlements.BedService));
        occupationTargetCatalog.Register(new CraftStationOccupationTargetSource(settlements.CraftStationService, anchorOccupationPlanBuilder));
        occupationTargetCatalog.Register(new ConstructionWorkPostOccupationTargetSource(
            constructionBootstrap.RuntimeApi,
            settlements.CraftStationService,
            anchorOccupationPlanBuilder));

        var occupationClaimRegistry = new OccupationClaimRegistry();
        occupationClaimRegistry.Register(new PublicSeatOccupationClaimSource(
            settlements.SeatService,
            occupationTargetCatalog,
            settlements.ZoneRuntimeService));

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
            settlements.WaypointService,
            navigationService,
            occupationLifecycleStrategyRegistry,
            occupationSustainStrategyRegistry);

        var occupationService = new ResidentOccupationService(
            residentRuntimeService,
            settlements.WaypointService,
            navigationService,
            occupationExecutionService,
            occupationResolverRegistry);

        var residentPresenceService = new ResidentPresenceService(
            residentCatalogService,
            residentRuntimeService,
            spawnService,
            settlements.SeatService,
            settlements.WaypointService,
            occupationService,
            occupationResolverRegistry,
            residentVisualService);

        var residentAssignmentService = new ResidentAssignmentService(
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            settlements.CraftStationService,
            constructionBootstrap.RuntimeApi,
            residentRuntimeService,
            scheduleService,
            occupationService,
            residentCatalogService,
            residentVisualService);

        var residentService = new RegistryResidentService(
            log,
            modeService.State,
            settlements.SlotService,
            settlements.SeatService,
            settlements.BedService,
            settlements.CraftStationService,
            residentRuntimeService,
            identityGenerator,
            customizationApplier,
            residentCatalogService,
            residentVisualService,
            residentPresenceService,
            residentAssignmentService,
            scheduleService);

        return new RegistryResidentsBootstrap(
            identityGenerator,
            customizationApplier,
            spawnService,
            occupationNavigationStrategyRegistry,
            occupationLifecycleStrategyRegistry,
            occupationSustainStrategyRegistry,
            navigationService,
            residentRuntimeService,
            residentCatalogService,
            residentVisualService,
            scheduleService,
            anchorOccupationPlanBuilder,
            occupationTargetCatalog,
            occupationClaimRegistry,
            occupationResolverRegistry,
            occupationExecutionService,
            occupationService,
            residentPresenceService,
            residentAssignmentService,
            residentService);
    }
}
