using System;
using BepInEx.Logging;
using HarmonyLib;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Registry.Bootstrap;
using Wyrdrasil.Registry.Patches;
using Wyrdrasil.Registry.PlayerTool.Visual;
using Wyrdrasil.Routines;

namespace Wyrdrasil.Registry;

public static class RegistryModuleBootstrap
{
    private const string DisabledPieceHighlightPatchTypeName =
        "Wyrdrasil.Registry.Patches.WyrdrasilRegistryPlayerToolPieceHighlightPatch";

    public static void ApplyHarmony(ManualLogSource log, Harmony harmony)
    {
        var assembly = typeof(RegistryModuleBootstrap).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (string.Equals(type.FullName, DisabledPieceHighlightPatchTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!HasHarmonyPatchAttribute(type))
            {
                continue;
            }

            harmony.CreateClassProcessor(type).Patch();
        }

        WyrdrasilRegistryPlayerToolUpdateHoverPatchInstaller.Apply(harmony, log);
        WyrdrasilWearNTearHighlightPatch.Apply(harmony, log);
    }

    private static bool HasHarmonyPatchAttribute(Type type)
    {
        return type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0;
    }

    public static RegistryModuleRuntime Create(
        ManualLogSource log,
        Harmony harmony,
        string pluginLocation,
        RegistryPlayerToolVisualConfig playerToolVisualConfig)
    {
        ApplyHarmony(log, harmony);
        RoutinesModuleBootstrap.ApplyHarmony(harmony);

        var modeService = new Wyrdrasil.Core.Services.RegistryModeService(log);
        var constructionBootstrap = ConstructionModuleBootstrap.Create(log, modeService);
        var settlementsBootstrap = RegistrySettlementsBootstrap.Create(log, modeService);
        var residentsBootstrap = RegistryResidentsBootstrap.Create(log, settlementsBootstrap, constructionBootstrap, modeService);

        var runtimeBootstrap = RegistryRuntimeBootstrap.Create(
            log,
            pluginLocation,
            playerToolVisualConfig,
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
            runtimeBootstrap.CraftStationIntegrityService,
            runtimeBootstrap.RegistryPlayerToolItemService,
            runtimeBootstrap.RegistryPlayerToolRuntimeService,
            runtimeBootstrap.RegistryPlayerToolCommandMessageService,
            runtimeBootstrap.RegistryToolController);
    }
}
