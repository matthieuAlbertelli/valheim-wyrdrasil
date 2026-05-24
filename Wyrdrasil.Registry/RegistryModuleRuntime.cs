using Wyrdrasil.Construction.Services;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Controllers;
using Wyrdrasil.Registry.PlayerTool;
using Wyrdrasil.Registry.Services;

namespace Wyrdrasil.Registry;

public sealed class RegistryModuleRuntime
{
    private readonly RegistryPersistenceService _persistenceService;
    private readonly ResidentRoutineService _residentRoutineService;
    private readonly ConstructionProjectIntegrityService _constructionProjectIntegrityService;
    private readonly ConstructionProjectProgressService _constructionProjectProgressService;
    private readonly ConstructionProjectMarkerService _constructionProjectMarkerService;
    private readonly ConstructionProjectGhostService _constructionProjectGhostService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionLinkVisualService _constructionLinkVisualService;
    private readonly RegistryPlayerToolItemService _registryPlayerToolItemService;
    private readonly RegistryPlayerToolRuntimeService _registryPlayerToolRuntimeService;
    private readonly RegistryToolController _registryToolController;

    public RegistryModuleRuntime(
        RegistryPersistenceService persistenceService,
        ResidentRoutineService residentRoutineService,
        ConstructionProjectIntegrityService constructionProjectIntegrityService,
        ConstructionProjectProgressService constructionProjectProgressService,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionProjectGhostService constructionProjectGhostService,
        ConstructionProjectService constructionProjectService,
        ConstructionLinkVisualService constructionLinkVisualService,
        RegistryPlayerToolItemService registryPlayerToolItemService,
        RegistryPlayerToolRuntimeService registryPlayerToolRuntimeService,
        RegistryToolController registryToolController)
    {
        _persistenceService = persistenceService;
        _residentRoutineService = residentRoutineService;
        _constructionProjectIntegrityService = constructionProjectIntegrityService;
        _constructionProjectProgressService = constructionProjectProgressService;
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _constructionProjectGhostService = constructionProjectGhostService;
        _constructionProjectService = constructionProjectService;
        _constructionLinkVisualService = constructionLinkVisualService;
        _registryPlayerToolItemService = registryPlayerToolItemService;
        _registryPlayerToolRuntimeService = registryPlayerToolRuntimeService;
        _registryToolController = registryToolController;
    }

    public void Update()
    {
        WyrdrasilPlayerCraftDebugMonitor.EnsureAttached(Player.m_localPlayer);
        _registryPlayerToolItemService.Update();
        _registryPlayerToolRuntimeService.Update();
        _persistenceService.Update();
        _residentRoutineService.Update();
        _constructionProjectIntegrityService.Update();
        _constructionProjectProgressService.Update();
        _constructionProjectMarkerService.Update();
        _constructionProjectService.PruneCompletedProjects();
        _registryToolController.Update();
        _constructionProjectGhostService.Update();
        _constructionLinkVisualService.Update();
    }

    public void OnGUI()
    {
        _registryToolController.OnGUI();
    }

    public void Shutdown()
    {
        _constructionProjectMarkerService.Reset();
        _constructionProjectGhostService.Reset();
        _constructionLinkVisualService.Reset();
        _persistenceService.SaveWorldState();
    }
}
