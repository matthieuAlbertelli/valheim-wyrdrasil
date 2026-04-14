using BepInEx;
using HarmonyLib;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Registry.Bootstrap;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Routines;
using Wyrdrasil.Routines.Services;

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
    private ConstructionProjectProgressService _constructionProjectProgressService = null!;
    private ConstructionProjectMarkerService _constructionProjectMarkerService = null!;
    private ConstructionProjectService _constructionProjectService = null!;
    private ConstructionLinkVisualService _constructionLinkVisualService = null!;
    private Harmony? _harmony;

    private void Awake()
    {
        _harmony = new Harmony(PluginGuid);
        RegistryModuleBootstrap.ApplyHarmony(_harmony);
        RoutinesModuleBootstrap.ApplyHarmony(_harmony);

        var constructionBootstrap = ConstructionModuleBootstrap.Create(Logger);
        _constructionProjectProgressService = constructionBootstrap.ConstructionProjectProgressService;
        _constructionProjectMarkerService = constructionBootstrap.ConstructionProjectMarkerService;
        _constructionProjectService = constructionBootstrap.ConstructionProjectService;

        var modeService = new RegistryModeService(Logger);
        var settlementsBootstrap = RegistrySettlementsBootstrap.Create(Logger, modeService);
        var residentsBootstrap = RegistryResidentsBootstrap.Create(Logger, settlementsBootstrap, constructionBootstrap, modeService);

        _worldClockService = new WorldClockService();
        _residentRoutineService = new ResidentRoutineService(
            Logger,
            _worldClockService,
            residentsBootstrap.ResidentService,
            residentsBootstrap.ResidentRuntimeService,
            residentsBootstrap.OccupationService);

        var runtimeBootstrap = RegistryRuntimeBootstrap.Create(
            Logger,
            modeService,
            settlementsBootstrap,
            residentsBootstrap,
            constructionBootstrap,
            _worldClockService,
            _residentRoutineService);

        _persistenceService = runtimeBootstrap.PersistenceService;
        _constructionLinkVisualService = runtimeBootstrap.ConstructionLinkVisualService;
        _registryToolController = runtimeBootstrap.RegistryToolController;

        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        WyrdrasilPlayerCraftDebugMonitor.EnsureAttached(Player.m_localPlayer);
        _persistenceService.Update();
        _residentRoutineService.Update();
        _constructionProjectProgressService.Update();
        _constructionProjectMarkerService.Update();
        _constructionProjectService.PruneCompletedProjects();
        _registryToolController.Update();
        _constructionLinkVisualService.Update();
    }

    private void OnGUI()
    {
        _registryToolController.OnGUI();
    }

    private void OnDestroy()
    {
        _constructionProjectMarkerService?.Reset();
        _constructionLinkVisualService?.Reset();
        _persistenceService?.SaveWorldState();
        _harmony?.UnpatchSelf();
    }
}
