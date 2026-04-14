using System.Collections.Generic;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Services;

public sealed class ResidentScheduleService
{
    private const int SleepPriority = 200;
    private const int WorkPriority = 100;
    private const int MealPriority = 150;
    private const int WanderPriority = 10;

    public void EnsureDefaultAutonomySchedules(RegisteredNpcData resident)
    {
        ApplyDefaultWanderSchedule(resident);
        ApplyDefaultPublicMealSchedule(resident);
    }

    public void ApplyDefaultWanderSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.WanderBetweenWaypoints,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.WanderBetweenWaypoints, 0, 0, WanderPriority)
            });
    }

    public void ApplyDefaultAssignedWorkSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedSlot);
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedCraftStation);
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.WorkAtAssignedTarget,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.WorkAtAssignedTarget, 10 * 60, 22 * 60, WorkPriority)
            });
    }

    public void ApplyDefaultInnkeeperSchedule(RegisteredNpcData resident)
    {
        ApplyDefaultAssignedWorkSchedule(resident);
    }

    public void ApplyDefaultCraftStationWorkSchedule(RegisteredNpcData resident)
    {
        ApplyDefaultAssignedWorkSchedule(resident);
    }

    public void ApplyDefaultConstructionWorkSchedule(RegisteredNpcData resident)
    {
        ApplyDefaultAssignedWorkSchedule(resident);
    }

    public void ApplyDefaultPublicMealSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.SitAtAvailablePublicSeat,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SitAtAvailablePublicSeat, 12 * 60, 13 * 60, MealPriority)
            });
    }

    public void ApplyDefaultSeatMealSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.SitAtAssignedSeat,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SitAtAssignedSeat, 12 * 60, 13 * 60, MealPriority + 5)
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

    public void ClearAssignedWorkSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedTarget);
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedSlot);
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedCraftStation);
    }

    public void ClearSlotSchedule(RegisteredNpcData resident)
    {
        ClearAssignedWorkSchedule(resident);
    }

    public void ClearCraftStationSchedule(RegisteredNpcData resident)
    {
        ClearAssignedWorkSchedule(resident);
    }

    public void ClearConstructionWorkSchedule(RegisteredNpcData resident)
    {
        ClearAssignedWorkSchedule(resident);
    }

    public void ClearAssignedSeatSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.SitAtAssignedSeat);
    }

    public void ClearPublicMealSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.SitAtAvailablePublicSeat);
    }

    public void ClearBedSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.SleepAtAssignedBed);
    }
}
