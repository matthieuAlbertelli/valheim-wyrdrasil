using System.Collections.Generic;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Services;


public sealed class RegistryResidentScheduleService
{
    private const int SleepPriority = 200;
    private const int WorkPriority = 100;
    private const int MealPriority = 50;

    public void ApplyDefaultInnkeeperSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.WorkAtAssignedSlot,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.WorkAtAssignedSlot, 10 * 60, 22 * 60, WorkPriority)
            });
    }

    public void ApplyDefaultSeatMealSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.SitAtAssignedSeat,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SitAtAssignedSeat, 12 * 60, 14 * 60, MealPriority)
            });
    }

    public void ApplyDefaultBedSleepSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.SleepAtAssignedBed,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SleepAtAssignedBed, 22 * 60, 6 * 60, SleepPriority)
            });
    }

    public void ClearSlotSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedSlot);
    }

    public void ClearSeatSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.SitAtAssignedSeat);
    }

    public void ClearBedSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.SleepAtAssignedBed);
    }
}
