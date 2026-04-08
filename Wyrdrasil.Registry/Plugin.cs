using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Registry.UI;
using Wyrdrasil.Routines;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Services;

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

        var appearanceCatalog = new NpcAppearanceCatalog();
        var equipmentCatalog = new NpcEquipmentCatalog();
        var appearanceGenerator = new NpcAppearanceGenerator(appearanceCatalog);
        var equipmentGenerator = new NpcEquipmentGenerator(equipmentCatalog);
        var identityGenerator = new NpcIdentityGenerator(appearanceGenerator, equipmentGenerator);
        var customizationApplier = new NpcCustomizationApplier(Logger);
        WyrdrasilVikingVisualBootstrap.ConfigureLogger(Logger);

        var vikingPrefabFactory = new VikingPrefabFactory(Logger);
        var spawnService = new NpcSpawnService(Logger, vikingPrefabFactory, identityGenerator, customizationApplier);
        var navigationService = new NpcNavigationService(Logger);
        var residentRuntimeService = new ResidentRuntimeService(Logger);
        var residentCatalogService = new ResidentCatalogService();
        var residentVisualService = new ResidentVisualService(modeService, residentRuntimeService);
        var scheduleService = new ResidentScheduleService();

        var occupationService = new ResidentOccupationService(
            Logger,
            residentRuntimeService,
            slotService,
            seatService,
            bedService,
            navigationService,
            waypointService);

        var residentPresenceService = new ResidentPresenceService(
            residentCatalogService,
            residentRuntimeService,
            spawnService,
            slotService,
            seatService,
            bedService,
            waypointService,
            occupationService,
            residentVisualService);

        var residentAssignmentService = new ResidentAssignmentService(
            slotService,
            seatService,
            bedService,
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
        var deletionService = new RegistryDeletionService(
            Logger,
            buildingService,
            zoneService,
            slotService,
            seatService,
            bedService,
            waypointService,
            residentService);

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
                bedService),

            new RegistrySoulsPersistenceParticipant(residentService),
            new RoutinesPersistenceParticipant(_worldClockService)
        };

        _persistenceService = new RegistryPersistenceService(
            Logger,
            slotService,
            seatService,
            bedService,
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
            waypointService,
            residentService,
            _persistenceService);

        var selectionService = new ToolSelectionService(modeService.State);
        var actionRegistry = BuildActionRegistry();

        var context = new RegistryContext(
            Logger,
            zoneService,
            waypointService,
            slotService,
            seatService,
            bedService,
            spawnService,
            residentService,
            _diagnosticsService,
            deletionService,
            flushService,
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
            residentService,
            _worldClockService,
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
        registry.Register(new DeleteSlotAction());
        registry.Register(new DeleteDesignatedSeatAction());
        registry.Register(new DeleteDesignatedBedAction());
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
        registry.Register(new SimulateNoonAction());
        registry.Register(new SimulateNightAction());
        registry.Register(new ClearTimeSimulationAction());
        registry.Register(new FlushRegistryStateAction());
        registry.Register(new LoggingRegistryAction(RegistryActionType.None));
        return registry;
    }

    private void Update()
    {
        _persistenceService.Update();
        _residentRoutineService.Update();
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