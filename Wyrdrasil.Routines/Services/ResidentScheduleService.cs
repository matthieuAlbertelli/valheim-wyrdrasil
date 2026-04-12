using System.Collections.Generic;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Services;


public sealed class ResidentScheduleService
{
    private const int SleepPriority = 200;
    private const int WorkPriority = 100;
    private const int MealPriority = 50;
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

    public void ApplyDefaultInnkeeperSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.WorkAtAssignedSlot,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.WorkAtAssignedSlot, 10 * 60, 22 * 60, WorkPriority)
            });
    }

    public void ApplyDefaultCraftStationWorkSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.WorkAtAssignedCraftStation,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.WorkAtAssignedCraftStation, 10 * 60, 22 * 60, WorkPriority)
            });
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

    public void ClearSlotSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedSlot);
    }

    public void ClearCraftStationSchedule(RegisteredNpcData resident)
    {
        resident.RemoveScheduleEntries(ResidentRoutineActivityType.WorkAtAssignedCraftStation);
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
