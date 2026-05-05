using BepInEx.Logging;
using HarmonyLib;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Bootstrap;
using Wyrdrasil.Souls.Bootstrap;

namespace Wyrdrasil.Routines;

public sealed class RoutinesModuleBootstrap
{
    public RoutinesModuleBootstrap(
        WorldClockService worldClockService,
        OccupationNavigationStrategyRegistry occupationNavigationStrategyRegistry,
        OccupationLifecycleStrategyRegistry occupationLifecycleStrategyRegistry,
        OccupationSustainStrategyRegistry occupationSustainStrategyRegistry,
        NpcNavigationService navigationService,
        ResidentScheduleService scheduleService,
        AnchorOccupationPlanBuilder anchorOccupationPlanBuilder,
        OccupationTargetFactory occupationTargetFactory,
        OccupationTargetCatalog occupationTargetCatalog,
        OccupationClaimRegistry occupationClaimRegistry,
        OccupationResolverRegistry occupationResolverRegistry,
        OccupationExecutionService occupationExecutionService,
        ResidentOccupationService occupationService,
        RoutinesPersistenceParticipant persistenceParticipant,
        IRoutinesRuntimeApi runtimeApi)
    {
        WorldClockService = worldClockService;
        OccupationNavigationStrategyRegistry = occupationNavigationStrategyRegistry;
        OccupationLifecycleStrategyRegistry = occupationLifecycleStrategyRegistry;
        OccupationSustainStrategyRegistry = occupationSustainStrategyRegistry;
        NavigationService = navigationService;
        ScheduleService = scheduleService;
        AnchorOccupationPlanBuilder = anchorOccupationPlanBuilder;
        OccupationTargetFactory = occupationTargetFactory;
        OccupationTargetCatalog = occupationTargetCatalog;
        OccupationClaimRegistry = occupationClaimRegistry;
        OccupationResolverRegistry = occupationResolverRegistry;
        OccupationExecutionService = occupationExecutionService;
        OccupationService = occupationService;
        PersistenceParticipant = persistenceParticipant;
        RuntimeApi = runtimeApi;
    }

    public WorldClockService WorldClockService { get; }
    public OccupationNavigationStrategyRegistry OccupationNavigationStrategyRegistry { get; }
    public OccupationLifecycleStrategyRegistry OccupationLifecycleStrategyRegistry { get; }
    public OccupationSustainStrategyRegistry OccupationSustainStrategyRegistry { get; }
    public NpcNavigationService NavigationService { get; }
    public ResidentScheduleService ScheduleService { get; }
    public AnchorOccupationPlanBuilder AnchorOccupationPlanBuilder { get; }
    public OccupationTargetFactory OccupationTargetFactory { get; }
    public OccupationTargetCatalog OccupationTargetCatalog { get; }
    public OccupationClaimRegistry OccupationClaimRegistry { get; }
    public OccupationResolverRegistry OccupationResolverRegistry { get; }
    public OccupationExecutionService OccupationExecutionService { get; }
    public ResidentOccupationService OccupationService { get; }
    public RoutinesPersistenceParticipant PersistenceParticipant { get; }
    public IRoutinesRuntimeApi RuntimeApi { get; }

    public static void ApplyHarmony(Harmony harmony)
    {
        harmony.PatchAll(typeof(RoutinesModuleBootstrap).Assembly);
    }

    public static RoutinesModuleBootstrap Create(
        ManualLogSource log,
        SettlementsModuleBootstrap settlementsModule,
        SoulsModuleBootstrap soulsModule)
    {
        var worldClockService = new WorldClockService();

        var occupationNavigationStrategyRegistry = new OccupationNavigationStrategyRegistry();
        occupationNavigationStrategyRegistry.Register(new StandOccupationNavigationStrategy());
        occupationNavigationStrategyRegistry.Register(new AnchoredAttachmentOccupationNavigationStrategy(OccupationExecutionProfile.SeatStrategyId));
        occupationNavigationStrategyRegistry.Register(new AnchoredAttachmentOccupationNavigationStrategy(OccupationExecutionProfile.BedStrategyId));
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

        var navigationService = new NpcNavigationService(log, occupationNavigationStrategyRegistry);
        var scheduleService = new ResidentScheduleService();
        var anchorOccupationPlanBuilder = new AnchorOccupationPlanBuilder();
        var occupationTargetFactory = new OccupationTargetFactory(anchorOccupationPlanBuilder);

        var occupationTargetCatalog = new OccupationTargetCatalog();
        occupationTargetCatalog.Register(new SlotOccupationTargetSource(settlementsModule.SlotService, occupationTargetFactory));
        occupationTargetCatalog.Register(new SeatOccupationTargetSource(settlementsModule.SeatService, occupationTargetFactory));
        occupationTargetCatalog.Register(new BedOccupationTargetSource(settlementsModule.BedService, occupationTargetFactory));
        occupationTargetCatalog.Register(new CraftStationOccupationTargetSource(settlementsModule.CraftStationService, occupationTargetFactory));

        var occupationClaimRegistry = new OccupationClaimRegistry();
        occupationClaimRegistry.Register(new PublicSeatOccupationClaimSource(
            settlementsModule.SeatService,
            occupationTargetCatalog,
            settlementsModule.ZoneRuntimeService));

        var occupationResolverRegistry = new OccupationResolverRegistry();
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            Wyrdrasil.Souls.Tool.ResidentRoutineActivityType.WorkAtAssignedTarget,
            Wyrdrasil.Souls.Tool.ResidentAssignmentPurpose.Work,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            Wyrdrasil.Souls.Tool.ResidentRoutineActivityType.WorkAtAssignedSlot,
            Wyrdrasil.Souls.Tool.ResidentAssignmentPurpose.Work,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            Wyrdrasil.Souls.Tool.ResidentRoutineActivityType.WorkAtAssignedCraftStation,
            Wyrdrasil.Souls.Tool.ResidentAssignmentPurpose.Work,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            Wyrdrasil.Souls.Tool.ResidentRoutineActivityType.SitAtAssignedSeat,
            Wyrdrasil.Souls.Tool.ResidentAssignmentPurpose.Meal,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new AssignedPurposeOccupationResolver(
            Wyrdrasil.Souls.Tool.ResidentRoutineActivityType.SleepAtAssignedBed,
            Wyrdrasil.Souls.Tool.ResidentAssignmentPurpose.Sleep,
            occupationTargetCatalog));
        occupationResolverRegistry.Register(new ClaimedOccupationResolver(
            Wyrdrasil.Souls.Tool.ResidentRoutineActivityType.SitAtAvailablePublicSeat,
            occupationClaimRegistry));

        var occupationExecutionService = new OccupationExecutionService(
            soulsModule.ResidentRuntimeService,
            settlementsModule.WaypointService,
            navigationService,
            occupationLifecycleStrategyRegistry,
            occupationSustainStrategyRegistry);

        var occupationService = new ResidentOccupationService(
            soulsModule.ResidentRuntimeService,
            settlementsModule.WaypointService,
            navigationService,
            occupationExecutionService,
            occupationResolverRegistry);

        var runtimeApi = new RoutinesRuntimeApi(
            worldClockService,
            scheduleService,
            occupationService,
            occupationResolverRegistry);
        var persistenceParticipant = new RoutinesPersistenceParticipant(worldClockService);

        return new RoutinesModuleBootstrap(
            worldClockService,
            occupationNavigationStrategyRegistry,
            occupationLifecycleStrategyRegistry,
            occupationSustainStrategyRegistry,
            navigationService,
            scheduleService,
            anchorOccupationPlanBuilder,
            occupationTargetFactory,
            occupationTargetCatalog,
            occupationClaimRegistry,
            occupationResolverRegistry,
            occupationExecutionService,
            occupationService,
            persistenceParticipant,
            runtimeApi);
    }
}
