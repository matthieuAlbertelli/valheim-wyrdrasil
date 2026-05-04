using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Runtime;

public sealed class RoutinesRuntimeApi : IRoutinesRuntimeApi
{
    private readonly WorldClockService _worldClockService;
    private readonly ResidentScheduleService _scheduleService;
    private readonly ResidentOccupationService _occupationService;
    private readonly OccupationResolverRegistry _resolverRegistry;

    public RoutinesRuntimeApi(
        WorldClockService worldClockService,
        ResidentScheduleService scheduleService,
        ResidentOccupationService occupationService,
        OccupationResolverRegistry resolverRegistry)
    {
        _worldClockService = worldClockService;
        _scheduleService = scheduleService;
        _occupationService = occupationService;
        _resolverRegistry = resolverRegistry;
    }

    public bool IsTimeSimulationActive => _worldClockService.IsSimulationActive;
    public bool TryGetCurrentMinuteOfDay(out int minuteOfDay) => _worldClockService.TryGetCurrentMinuteOfDay(out minuteOfDay);
    public string GetClockLabel() => _worldClockService.GetClockLabel();
    public string GetClockModeLabel() => _worldClockService.GetClockModeLabel();
    public void SimulateNoon() => _worldClockService.SimulateNoon();
    public void SimulateNight() => _worldClockService.SimulateNight();
    public void ClearTimeSimulation() => _worldClockService.ClearSimulation();
    public void EnsureDefaultAutonomySchedules(RegisteredNpcData resident) => _scheduleService.EnsureDefaultAutonomySchedules(resident);
    public void ApplyDefaultInnkeeperSchedule(RegisteredNpcData resident) => _scheduleService.ApplyDefaultInnkeeperSchedule(resident);
    public void ApplyDefaultSeatMealSchedule(RegisteredNpcData resident) => _scheduleService.ApplyDefaultSeatMealSchedule(resident);
    public void ApplyDefaultBedSleepSchedule(RegisteredNpcData resident) => _scheduleService.ApplyDefaultBedSleepSchedule(resident);
    public void ApplyDefaultCraftStationWorkSchedule(RegisteredNpcData resident) => _scheduleService.ApplyDefaultCraftStationWorkSchedule(resident);
    public void ApplyDefaultConstructionWorkSchedule(RegisteredNpcData resident) => _scheduleService.ApplyDefaultConstructionWorkSchedule(resident);
    public void ClearSlotSchedule(RegisteredNpcData resident) => _scheduleService.ClearSlotSchedule(resident);
    public void ClearCraftStationSchedule(RegisteredNpcData resident) => _scheduleService.ClearCraftStationSchedule(resident);
    public void ClearConstructionWorkSchedule(RegisteredNpcData resident) => _scheduleService.ClearConstructionWorkSchedule(resident);
    public void ClearAssignedSeatSchedule(RegisteredNpcData resident) => _scheduleService.ClearAssignedSeatSchedule(resident);
    public void ClearBedSchedule(RegisteredNpcData resident) => _scheduleService.ClearBedSchedule(resident);
    public bool TryStartOccupation(RegisteredNpcData resident, ResidentRoutineActivityType activityType) => _occupationService.TryStartOccupation(resident, activityType);
    public void ContinueOccupation(RegisteredNpcData resident, ResidentRoutineActivityType activityType) => _occupationService.ContinueOccupation(resident, activityType);
    public void ReleaseOccupation(RegisteredNpcData resident, bool detachIfAttached = true) => _occupationService.ReleaseOccupation(resident, detachIfAttached);
    public bool TryResolveActivityTarget(RegisteredNpcData resident, ResidentRoutineActivityType activityType, out OccupationTarget target)
    {
        if (_resolverRegistry.TryGetResolver(activityType, out var resolver) &&
            resolver.TryResolve(resident, out target))
        {
            return true;
        }

        target = null!;
        return false;
    }
}
