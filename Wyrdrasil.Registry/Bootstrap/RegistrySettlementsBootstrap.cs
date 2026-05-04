using BepInEx.Logging;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Bootstrap;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Bootstrap;

internal sealed class RegistrySettlementsBootstrap
{
    public RegistrySettlementsBootstrap(
        SettlementsModuleBootstrap moduleBootstrap,
        RegistrySettlementsCompositionServices services)
    {
        ModuleBootstrap = moduleBootstrap;
        Services = services;
        PersistenceParticipant = moduleBootstrap.PersistenceParticipant;
        AuthoringApi = moduleBootstrap.AuthoringApi;
        RuntimeApi = moduleBootstrap.RuntimeApi;
    }

    internal SettlementsModuleBootstrap ModuleBootstrap { get; }
    internal RegistrySettlementsCompositionServices Services { get; }
    internal SettlementsPersistenceParticipant PersistenceParticipant { get; }
    internal ISettlementsAuthoringApi AuthoringApi { get; }
    internal ISettlementsRuntimeApi RuntimeApi { get; }

    public static RegistrySettlementsBootstrap Create(ManualLogSource log, RegistryModeService modeService)
    {
        var moduleBootstrap = SettlementsModuleBootstrap.Create(log, modeService);
        var services = new RegistrySettlementsCompositionServices(
            moduleBootstrap.BuildingService,
            moduleBootstrap.ZoneService,
            moduleBootstrap.WaypointService,
            moduleBootstrap.SlotService,
            moduleBootstrap.SeatService,
            moduleBootstrap.BedService,
            moduleBootstrap.CraftStationService);

        return new RegistrySettlementsBootstrap(moduleBootstrap, services);
    }
}
