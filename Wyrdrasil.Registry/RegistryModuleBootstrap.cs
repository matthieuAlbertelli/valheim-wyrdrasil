using BepInEx.Logging;
using HarmonyLib;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Registry.Bootstrap;
using Wyrdrasil.Routines;

namespace Wyrdrasil.Registry;

public static class RegistryModuleBootstrap
{
    public static void ApplyHarmony(Harmony harmony)
    {
        harmony.PatchAll(typeof(RegistryModuleBootstrap).Assembly);
    }

    public static RegistryModuleRuntime Create(ManualLogSource log, Harmony harmony)
    {
        ApplyHarmony(harmony);
        RoutinesModuleBootstrap.ApplyHarmony(harmony);

        var modeService = new Wyrdrasil.Core.Services.RegistryModeService(log);
        var constructionBootstrap = ConstructionModuleBootstrap.Create(log, modeService);
        var settlementsBootstrap = RegistrySettlementsBootstrap.Create(log, modeService);
        var residentsBootstrap = RegistryResidentsBootstrap.Create(log, settlementsBootstrap, constructionBootstrap, modeService);

        var runtimeBootstrap = RegistryRuntimeBootstrap.Create(
            log,
            modeService,
            settlementsBootstrap,
            residentsBootstrap,
            constructionBootstrap);

        return new RegistryModuleRuntime(
            runtimeBootstrap.PersistenceService,
            residentsBootstrap.Services.ResidentRoutineService,
            constructionBootstrap.ConstructionProjectIntegrityService,
            constructionBootstrap.ConstructionProjectProgressService,
            constructionBootstrap.ConstructionProjectMarkerService,
            constructionBootstrap.ConstructionProjectGhostService,
            constructionBootstrap.ConstructionProjectService,
            runtimeBootstrap.ConstructionLinkVisualService,
            runtimeBootstrap.RegistryToolController);
    }
}
