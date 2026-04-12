namespace Wyrdrasil.Registry.Tool;

public enum RegistryActionType
{
    None,

    CreateTavernZone,
    CreateBedroomZone,
    CreateNavigationWaypoint,
    ConnectNavigationWaypoints,
    DeleteNavigationWaypoint,
    DeleteZone,

    CreateInnkeeperSlot,
    DesignateSeatFurniture,
    DesignateBedFurniture,
    DesignateCraftStationFurniture,
    DeleteSlot,
    DeleteDesignatedSeat,
    DeleteDesignatedBed,
    DeleteDesignatedCraftStation,

    RegisterNpc,
    AssignInnkeeperRole,
    AssignSeat,
    AssignBed,
    ClearTargetInnkeeperSlotAssignment,
    ClearTargetSeatAssignment,
    ClearTargetBedAssignment,
    ForceAssignResident,
    DespawnTargetResident,
    RespawnAssignedResident,
    SpawnTestViking,

    InspectTargetNpcAi,
    EditTargetCraftStationAnchor,
    ProbeAssignedCraftStationOccupation,
    SimulateNoon,
    SimulateNight,
    ClearTimeSimulation,
    FlushRegistryState
}
