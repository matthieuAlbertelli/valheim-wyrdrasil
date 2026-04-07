using BepInEx;
using HarmonyLib;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Registry.UI;

namespace Wyrdrasil.Registry;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.wyrdrasil.registry";
    public const string PluginName = "Wyrdrasil.Registry";
    public const string PluginVersion = "0.1.0";

    private RegistryToolController _registryToolController = null!;
    private Harmony? _harmony;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        var modeService = new RegistryModeService(Logger);
        var buildingService = new RegistryBuildingService(Logger);
        var anchorPolicyService = new RegistryAnchorPolicyService();
        var zoneService = new RegistryZoneService(Logger, modeService, buildingService);
        var waypointService = new RegistryWaypointService(Logger, modeService, zoneService);
        var slotService = new RegistrySlotService(Logger, modeService, zoneService, anchorPolicyService);
        var seatService = new RegistrySeatService(Logger, modeService, zoneService, anchorPolicyService);

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
        var residentRuntimeService = new RegistryResidentRuntimeService(Logger, navigationService, waypointService);
        var residentService = new RegistryResidentService(
            Logger,
            modeService.State,
            modeService,
            slotService,
            seatService,
            residentRuntimeService,
            spawnService,
            identityGenerator,
            customizationApplier);

        var diagnosticsService = new RegistryDiagnosticsService(Logger);
        var deletionService = new RegistryDeletionService(Logger, buildingService, zoneService, slotService, seatService, waypointService, residentService);

        var selectionService = new RegistrySelectionService(modeService.State);
        var actionRegistry = BuildActionRegistry();
        var context = new RegistryContext(Logger, buildingService, zoneService, waypointService, slotService, seatService, spawnService, residentService, diagnosticsService, deletionService);
        var hudRenderer = new RegistryHudRenderer();

        _registryToolController = new RegistryToolController(
            modeService,
            selectionService,
            actionRegistry,
            context,
            hudRenderer);

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private static RegistryActionRegistry BuildActionRegistry()
    {
        var registry = new RegistryActionRegistry();

        registry.Register(new CreateTavernZoneAction());
        registry.Register(new CreateTavernWaypointAction());
        registry.Register(new ConnectNavigationWaypointsAction());
        registry.Register(new DeleteNavigationWaypointAction());
        registry.Register(new DeleteZoneAction());
        registry.Register(new CreateInnkeeperSlotAction());
        registry.Register(new DesignateSeatFurnitureAction());
        registry.Register(new DeleteSlotAction());
        registry.Register(new DeleteDesignatedSeatAction());
        registry.Register(new SpawnTestVikingAction());
        registry.Register(new RegisterNpcAction());
        registry.Register(new AssignInnkeeperRoleAction());
        registry.Register(new AssignSeatAction());
        registry.Register(new ClearTargetInnkeeperSlotAssignmentAction());
        registry.Register(new ClearTargetSeatAssignmentAction());
        registry.Register(new ForceAssignResidentAction());
        registry.Register(new DespawnTargetResidentAction());
        registry.Register(new RespawnAssignedResidentAction());
        registry.Register(new InspectTargetNpcAiAction());
        registry.Register(new LoggingRegistryAction(RegistryActionType.None));

        return registry;
    }

    private void Update()
    {
        _registryToolController.Update();
    }

    private void OnGUI()
    {
        _registryToolController.OnGUI();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
