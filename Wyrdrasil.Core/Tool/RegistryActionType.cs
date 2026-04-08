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
    DeleteSlot,
    DeleteDesignatedSeat,
    DeleteDesignatedBed,

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
    SimulateNoon,
    SimulateNight,
    ClearTimeSimulation,
    FlushRegistryState
}
