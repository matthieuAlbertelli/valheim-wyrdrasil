using BepInEx.Logging;
using Wyrdrasil.Construction.Bootstrap;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Core.Persistence;
using Wyrdrasil.Registry.Occupations;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Routines;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Souls.Authoring;
using Wyrdrasil.Souls.Bootstrap;
using Wyrdrasil.Souls.Runtime;

namespace Wyrdrasil.Registry.Bootstrap;

internal sealed class RegistryResidentsBootstrap
{
    public RegistryResidentsBootstrap(
        SoulsModuleBootstrap moduleBootstrap,
        RoutinesModuleBootstrap routinesModuleBootstrap,
        IWorldPersistenceParticipant soulsPersistenceParticipant,
        RegistryResidentsCompositionServices services)
    {
        ModuleBootstrap = moduleBootstrap;
        RoutinesModuleBootstrap = routinesModuleBootstrap;
        SoulsPersistenceParticipant = soulsPersistenceParticipant;
        Services = services;
        AuthoringApi = moduleBootstrap.AuthoringApi;
        RuntimeApi = moduleBootstrap.RuntimeApi;
        RoutinesRuntimeApi = routinesModuleBootstrap.RuntimeApi;
    }

    internal SoulsModuleBootstrap ModuleBootstrap { get; }
    internal RoutinesModuleBootstrap RoutinesModuleBootstrap { get; }
    internal IWorldPersistenceParticipant SoulsPersistenceParticipant { get; }
    internal IWorldPersistenceParticipant RoutinesPersistenceParticipant => RoutinesModuleBootstrap.PersistenceParticipant;
    internal RegistryResidentsCompositionServices Services { get; }
    internal ISoulsAuthoringApi AuthoringApi { get; }
    internal ISoulsRuntimeApi RuntimeApi { get; }
    internal IRoutinesRuntimeApi RoutinesRuntimeApi { get; }

    public static RegistryResidentsBootstrap Create(
        ManualLogSource log,
        RegistrySettlementsBootstrap settlements,
        ConstructionModuleBootstrap constructionBootstrap,
        Wyrdrasil.Core.Services.RegistryModeService modeService)
    {
        var moduleBootstrap = SoulsModuleBootstrap.Create(log);
        var routinesModuleBootstrap = RoutinesModuleBootstrap.Create(log, settlements.ModuleBootstrap, moduleBootstrap);

        routinesModuleBootstrap.OccupationSustainStrategyRegistry.Register(
            new ConstructionWorkOccupationSustainStrategy(constructionBootstrap.RuntimeApi));

        routinesModuleBootstrap.OccupationTargetCatalog.Register(new ConstructionWorkPostOccupationTargetSource(
            constructionBootstrap.RuntimeApi,
            settlements.RuntimeApi,
            routinesModuleBootstrap.AnchorOccupationPlanBuilder));

        var residentVisualService = new ResidentVisualService(modeService, moduleBootstrap.RuntimeApi);

        var residentPresenceService = new ResidentPresenceService(
            moduleBootstrap.RuntimeApi,
            settlements.RuntimeApi,
            routinesModuleBootstrap.RuntimeApi,
            residentVisualService);

        var residentAssignmentService = new ResidentAssignmentService(
            settlements.RuntimeApi,
            constructionBootstrap.RuntimeApi,
            moduleBootstrap.RuntimeApi,
            routinesModuleBootstrap.RuntimeApi,
            residentVisualService);

        var residentService = new RegistryResidentService(
            log,
            modeService.State,
            settlements.AuthoringApi,
            settlements.RuntimeApi,
            moduleBootstrap.RuntimeApi,
            moduleBootstrap.AuthoringApi,
            routinesModuleBootstrap.RuntimeApi,
            residentVisualService,
            residentPresenceService,
            residentAssignmentService);

        var residentRoutineService = new ResidentRoutineService(
            log,
            routinesModuleBootstrap.RuntimeApi,
            residentService,
            moduleBootstrap.RuntimeApi);

        var services = new RegistryResidentsCompositionServices(
            residentVisualService,
            residentPresenceService,
            residentAssignmentService,
            residentService,
            residentRoutineService);

        return new RegistryResidentsBootstrap(moduleBootstrap, routinesModuleBootstrap, moduleBootstrap.PersistenceParticipant, services);
    }

    internal IWorldPersistenceRestoreHook CreatePersistenceRestoreHook(
        ISettlementsRuntimeApi settlementsRuntimeApi,
        IConstructionRuntimeApi constructionRuntimeApi)
    {
        return new RegistryResidentPersistenceRestoreHook(
            settlementsRuntimeApi,
            constructionRuntimeApi,
            Services.ResidentService,
            Services.ResidentRoutineService);
    }
}
