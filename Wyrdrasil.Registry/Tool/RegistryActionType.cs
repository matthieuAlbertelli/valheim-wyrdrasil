namespace Wyrdrasil.Registry.Tool;

public enum RegistryActionType
{
    None,

    CreateTavernZone,
    CreateNavigationWaypoint,
    ConnectNavigationWaypoints,
    DeleteNavigationWaypoint,
    DeleteZone,

    CreateInnkeeperSlot,
    DesignateSeatFurniture,
    DeleteSlot,
    DeleteDesignatedSeat,

    RegisterNpc,
    AssignInnkeeperRole,
    AssignSeat,
    ClearTargetInnkeeperSlotAssignment,
    ClearTargetSeatAssignment,
    ForceAssignResident,
    DespawnTargetResident,
    RespawnAssignedResident,
    SpawnTestViking,

    InspectTargetNpcAi,
    FlushRegistryState
}
