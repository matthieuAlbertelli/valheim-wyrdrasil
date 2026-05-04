using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Runtime;

public interface IRoutinesRuntimeApi
{
    bool IsTimeSimulationActive { get; }
    bool TryGetCurrentMinuteOfDay(out int minuteOfDay);
    string GetClockLabel();
    string GetClockModeLabel();
    void SimulateNoon();
    void SimulateNight();
    void ClearTimeSimulation();
    void EnsureDefaultAutonomySchedules(RegisteredNpcData resident);
    void ApplyDefaultInnkeeperSchedule(RegisteredNpcData resident);
    void ApplyDefaultSeatMealSchedule(RegisteredNpcData resident);
    void ApplyDefaultBedSleepSchedule(RegisteredNpcData resident);
    void ApplyDefaultCraftStationWorkSchedule(RegisteredNpcData resident);
    void ApplyDefaultConstructionWorkSchedule(RegisteredNpcData resident);
    void ClearSlotSchedule(RegisteredNpcData resident);
    void ClearCraftStationSchedule(RegisteredNpcData resident);
    void ClearConstructionWorkSchedule(RegisteredNpcData resident);
    void ClearAssignedSeatSchedule(RegisteredNpcData resident);
    void ClearBedSchedule(RegisteredNpcData resident);
    bool TryStartOccupation(RegisteredNpcData resident, ResidentRoutineActivityType activityType);
    void ContinueOccupation(RegisteredNpcData resident, ResidentRoutineActivityType activityType);
    void ReleaseOccupation(RegisteredNpcData resident, bool detachIfAttached = true);
    bool TryResolveActivityTarget(RegisteredNpcData resident, ResidentRoutineActivityType activityType, out OccupationTarget target);
}
