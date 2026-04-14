using BepInEx.Logging;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Registry.Handlers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;

namespace Wyrdrasil.Registry.Bootstrap;

public static class RegistryActionRegistryFactory
{
    public static ActionRegistry CreateDefault(
        ManualLogSource log,
        FunctionalZoneService zoneService,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        CraftStationService craftStationService,
        NpcSpawnService spawnService,
        RegistryResidentService residentService,
        TargetDiagnosticsService diagnosticsService,
        CraftStationAnchorEditorService craftStationAnchorEditorService,
        RegistryDeletionService deletionService,
        RegistryFlushService flushService,
        IConstructionAuthoringApi constructionAuthoringApi,
        IConstructionTestingApi constructionTestingApi,
        IConstructionRuntimeApi constructionRuntimeApi,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionPlacementPreviewService constructionPlacementPreviewService,
        ConstructionDebugSessionService constructionDebugSessionService,
        WorldClockService worldClockService)
    {
        var registry = new ActionRegistry(log);
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateTavernZone, new CreateTavernZoneHandler(zoneService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateBedroomZone, new CreateBedroomZoneHandler(zoneService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateNavigationWaypoint, new CreateNavigationWaypointHandler(waypointService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ConnectNavigationWaypoints, new ConnectNavigationWaypointsHandler(waypointService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteNavigationWaypoint, new DeleteNavigationWaypointHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteZone, new DeleteZoneHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateInnkeeperSlot, new CreateInnkeeperSlotHandler(slotService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DesignateSeatFurniture, new DesignateSeatFurnitureHandler(seatService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DesignateBedFurniture, new DesignateBedFurnitureHandler(bedService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DesignateCraftStationFurniture, new DesignateCraftStationFurnitureHandler(craftStationService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteSlot, new DeleteSlotHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteDesignatedSeat, new DeleteDesignatedSeatHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteDesignatedBed, new DeleteDesignatedBedHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteDesignatedCraftStation, new DeleteDesignatedCraftStationHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SpawnTestViking, new SpawnTestVikingHandler(spawnService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.RegisterNpc, new RegisterNpcHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignInnkeeperRole, new AssignInnkeeperRoleHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignSeat, new AssignSeatHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignBed, new AssignBedHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetInnkeeperSlotAssignment, new ClearTargetInnkeeperSlotAssignmentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetSeatAssignment, new ClearTargetSeatAssignmentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetBedAssignment, new ClearTargetBedAssignmentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ForceAssignResident, new ForceAssignResidentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DespawnTargetResident, new DespawnTargetResidentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.RespawnAssignedResident, new RespawnAssignedResidentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.InspectTargetNpcAi, new InspectTargetNpcAiHandler(diagnosticsService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.EditTargetCraftStationAnchor, new EditTargetCraftStationAnchorHandler(craftStationAnchorEditorService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ProbeAssignedCraftStationOccupation, new ProbeAssignedCraftStationOccupationHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SimulateNoon, new SimulateNoonHandler(worldClockService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SimulateNight, new SimulateNightHandler(worldClockService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTimeSimulation, new ClearTimeSimulationHandler(worldClockService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CaptureBlueprintFromTargetZone, new CaptureBlueprintFromTargetZoneHandler(log, zoneService, constructionAuthoringApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SpawnTestConstructionProject, new SpawnTestConstructionProjectHandler(log, constructionAuthoringApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DumpLatestConstructionProjectState, new DumpLatestConstructionProjectStateHandler(log, constructionTestingApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignTargetCraftStationToConstructionProject, new AssignTargetCraftStationToConstructionProjectHandler(log, craftStationService, constructionRuntimeApi, constructionProjectMarkerService, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignTargetResidentToLatestConstructionProject, new AssignTargetResidentToLatestConstructionProjectHandler(log, residentService, constructionProjectMarkerService, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetResidentConstructionAssignment, new ClearTargetResidentConstructionAssignmentHandler(log, residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteConstructionInTargetZone, new DeleteConstructionInTargetZoneHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ForceCompleteLatestConstructionProject, new ForceCompleteLatestConstructionProjectHandler(log, constructionTestingApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ResetLatestConstructionProject, new ResetLatestConstructionProjectHandler(log, constructionTestingApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ToggleConstructionVerboseLogging, new ToggleConstructionVerboseLoggingHandler(log, constructionTestingApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.PlaceBlueprintInstantly, new PlaceBlueprintInstantlyHandler(log, zoneService, constructionPlacementPreviewService, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.FlushRegistryState, new FlushRegistryStateHandler(flushService)));
        registry.Register(new LoggingRegistryAction(RegistryActionType.None, log));
        return registry;
    }
}
