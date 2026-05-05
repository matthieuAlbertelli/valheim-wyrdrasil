using BepInEx.Logging;
using Wyrdrasil.Construction.Authoring;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Construction.Testing;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Registry.Actions;
using Wyrdrasil.Registry.Handlers;
using Wyrdrasil.Registry.Services;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Settlements.Authoring;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Authoring;

namespace Wyrdrasil.Registry.Bootstrap;

public static class RegistryActionRegistryFactory
{
    public static ActionRegistry CreateDefault(
        ManualLogSource log,
        ISettlementsAuthoringApi settlementsAuthoringApi,
        NavigationWaypointService waypointService,
        ZoneSlotService slotService,
        SeatService seatService,
        BedService bedService,
        ISettlementsAuthoringApi settlementsAuthoringApiForConstruction,
        ISoulsAuthoringApi soulsAuthoringApi,
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
        IRoutinesRuntimeApi routinesRuntimeApi)
    {
        var registry = new ActionRegistry(log);
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateTavernZone, new CreateTavernZoneHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateBedroomZone, new CreateBedroomZoneHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateNavigationWaypoint, new CreateNavigationWaypointHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ConnectNavigationWaypoints, new ConnectNavigationWaypointsHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteNavigationWaypoint, new DeleteNavigationWaypointHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteZone, new DeleteZoneHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CreateInnkeeperSlot, new CreateInnkeeperSlotHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DesignateSeatFurniture, new DesignateSeatFurnitureHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DesignateBedFurniture, new DesignateBedFurnitureHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DesignateCraftStationFurniture, new DesignateCraftStationFurnitureHandler(settlementsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteSlot, new DeleteSlotHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteDesignatedSeat, new DeleteDesignatedSeatHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteDesignatedBed, new DeleteDesignatedBedHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteDesignatedCraftStation, new DeleteDesignatedCraftStationHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SpawnTestViking, new SpawnTestVikingHandler(soulsAuthoringApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.RegisterNpc, new RegisterNpcHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignInnkeeperRole, new AssignInnkeeperRoleHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignSeat, new AssignSeatHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignBed, new AssignBedHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignCraftStation, new AssignCraftStationHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetInnkeeperSlotAssignment, new ClearTargetInnkeeperSlotAssignmentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetSeatAssignment, new ClearTargetSeatAssignmentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetBedAssignment, new ClearTargetBedAssignmentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetCraftStationAssignment, new ClearTargetCraftStationAssignmentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ForceAssignResident, new ForceAssignResidentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DespawnTargetResident, new DespawnTargetResidentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.RespawnAssignedResident, new RespawnAssignedResidentHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.InspectTargetNpcAi, new InspectTargetNpcAiHandler(diagnosticsService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.EditTargetCraftStationAnchor, new EditTargetCraftStationAnchorHandler(craftStationAnchorEditorService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ProbeAssignedCraftStationOccupation, new ProbeAssignedCraftStationOccupationHandler(residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SimulateNoon, new SimulateNoonHandler(routinesRuntimeApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SimulateNight, new SimulateNightHandler(routinesRuntimeApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTimeSimulation, new ClearTimeSimulationHandler(routinesRuntimeApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.CaptureBlueprintFromTargetZone, new CaptureBlueprintFromTargetZoneHandler(log, settlementsAuthoringApi, constructionAuthoringApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.SpawnTestConstructionProject, new SpawnTestConstructionProjectHandler(log, constructionAuthoringApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DumpLatestConstructionProjectState, new DumpLatestConstructionProjectStateHandler(log, constructionTestingApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignTargetCraftStationToConstructionProject, new AssignTargetCraftStationToConstructionProjectHandler(log, settlementsAuthoringApiForConstruction, constructionRuntimeApi, constructionProjectMarkerService, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.AssignTargetResidentToLatestConstructionProject, new AssignTargetResidentToLatestConstructionProjectHandler(log, residentService, constructionProjectMarkerService, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ClearTargetResidentConstructionAssignment, new ClearTargetResidentConstructionAssignmentHandler(log, residentService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.DeleteConstructionInTargetZone, new DeleteConstructionInTargetZoneHandler(deletionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ForceCompleteLatestConstructionProject, new ForceCompleteLatestConstructionProjectHandler(log, constructionTestingApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ResetLatestConstructionProject, new ResetLatestConstructionProjectHandler(log, constructionTestingApi, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.ToggleConstructionVerboseLogging, new ToggleConstructionVerboseLoggingHandler(log, constructionTestingApi)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.PlaceBlueprintInstantly, new PlaceBlueprintInstantlyHandler(log, settlementsAuthoringApi, constructionPlacementPreviewService, constructionDebugSessionService)));
        registry.Register(new HandlerBackedRegistryAction(RegistryActionType.FlushRegistryState, new FlushRegistryStateHandler(flushService)));
        registry.Register(new LoggingRegistryAction(RegistryActionType.None, log));
        return registry;
    }
}
