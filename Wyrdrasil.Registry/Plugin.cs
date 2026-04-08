using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Registry.UI;
using Wyrdrasil.Routines;

namespace Wyrdrasil.Registry;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.wyrdrasil.registry";
    public const string PluginName = "Wyrdrasil.Registry";
    public const string PluginVersion = "0.1.0";

    private RegistryToolController _registryToolController = null!;
    private RegistryPersistenceService _persistenceService = null!;
    private RegistryWorldClockService _worldClockService = null!;
    private RegistryResidentRoutineService _residentRoutineService = null!;
    private Harmony? _harmony;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);

        RegistryModuleBootstrap.ApplyHarmony(_harmony);
        RoutinesModuleBootstrap.ApplyHarmony(_harmony);

        var modeService = new RegistryModeService(Logger);
        var buildingService = new RegistryBuildingService(Logger);
        var anchorPolicyService = new RegistryAnchorPolicyService();
        var zoneService = new RegistryZoneService(Logger, modeService, buildingService);
        var waypointService = new RegistryWaypointService(Logger, modeService, zoneService);
        var slotService = new RegistrySlotService(Logger, modeService, zoneService, anchorPolicyService);
        var seatService = new RegistrySeatService(Logger, modeService, zoneService, anchorPolicyService);
        var bedService = new RegistryBedService(Logger, modeService, zoneService, anchorPolicyService);

        var appearanceCatalog = new RegistryNpcAppearanceCatalog();
        var equipmentCatalog = new RegistryNpcEquipmentCatalog();
        var appearanceGenerator = new RegistryNpcAppearanceGenerator(appearanceCatalog);
        var equipmentGenerator = new RegistryNpcEquipmentGenerator(equipmentCatalog);
        var identityGenerator = new RegistryNpcIdentityGenerator(appearanceGenerator, equipmentGenerator);
        var customizationApplier = new RegistryNpcCustomizationApplier(Logger);
        Wyrdrasil.Registry.Components.WyrdrasilVikingVisualBootstrap.ConfigureLogger(Logger);

        var vikingPrefabFactory = new RegistryVikingPrefabFactory(Logger);
        var spawnService = new RegistrySpawnService(Logger, vikingPrefabFactory, identityGenerator, customizationApplier);
        var navigationService = new RegistryNpcNavigationService(Logger);
        var residentRuntimeService = new RegistryResidentRuntimeService(Logger);
        var residentCatalogService = new RegistryResidentCatalogService();
        var residentVisualService = new RegistryResidentVisualService(modeService, residentRuntimeService);
        var scheduleService = new RegistryResidentScheduleService();
        var occupationService = new RegistryResidentOccupationService(
            Logger,
            residentRuntimeService,
            slotService,
            seatService,
            bedService,
            navigationService,
            waypointService);

        var residentPresenceService = new RegistryResidentPresenceService(
            residentCatalogService,
            residentRuntimeService,
            spawnService,
            slotService,
            seatService,
            bedService,
            occupationService,
            residentVisualService);

        var residentAssignmentService = new RegistryResidentAssignmentService(
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
            residentAssignmentService);

        _worldClockService = new RegistryWorldClockService();
        _residentRoutineService = new RegistryResidentRoutineService(
            Logger,
            _worldClockService,
            residentService,
            residentRuntimeService,
            occupationService);

        var diagnosticsService = new RegistryDiagnosticsService(Logger);
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
            new RegistrySettlementsPersistenceParticipant(
                Logger,
                buildingService,
                zoneService,
                waypointService,
                slotService,
                seatService,
                bedService),

            new RegistrySoulsPersistenceParticipant(residentService),
            new RegistryRoutinesPersistenceParticipant(_worldClockService)
        };

        _persistenceService = new RegistryPersistenceService(
            Logger,
            slotService,
            seatService,
            bedService,
            residentService,
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

        var selectionService = new RegistrySelectionService(modeService.State);
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
            diagnosticsService,
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

    private static RegistryActionRegistry BuildActionRegistry()
    {
        var registry = new RegistryActionRegistry();
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