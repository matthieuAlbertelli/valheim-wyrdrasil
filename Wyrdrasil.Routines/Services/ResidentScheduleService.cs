using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Services;

public sealed class ResidentScheduleService
{
    private const int SleepPriority = 200;
    private const int WorkPriority = 100;
    private const int MealPriority = 150;
    private const int WanderPriority = 10;

    private const int SunriseWorkStartMinute = 6 * 60;
    private const int NightWorkEndMinute = 22 * 60;
    private const int NoonMealStartMinute = 12 * 60;
    private const int NoonMealEndMinute = 13 * 60;
    private const int EveningMealStartMinute = 18 * 60;
    private const int EveningMealEndMinute = 20 * 60;

    public void EnsureDefaultAutonomySchedules(RegisteredNpcData resident)
    {
        ApplyDefaultWanderSchedule(resident);

        if (resident.Role == NpcRole.Innkeeper)
        {
            ClearPublicMealSchedule(resident);
            return;
        }

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
                new ResidentScheduleEntryData(ResidentRoutineActivityType.WorkAtAssignedTarget, SunriseWorkStartMinute, NightWorkEndMinute, WorkPriority)
            });
    }

    public void ApplyDefaultInnkeeperSchedule(RegisteredNpcData resident)
    {
        ClearAssignedSeatSchedule(resident);
        ClearPublicMealSchedule(resident);
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
        if (resident.Role == NpcRole.Innkeeper)
        {
            ClearPublicMealSchedule(resident);
            return;
        }

        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.SitAtAvailablePublicSeat,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SitAtAvailablePublicSeat, NoonMealStartMinute, NoonMealEndMinute, MealPriority),
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SitAtAvailablePublicSeat, EveningMealStartMinute, EveningMealEndMinute, MealPriority)
            });
    }

    public void ApplyDefaultSeatMealSchedule(RegisteredNpcData resident)
    {
        resident.ReplaceScheduleEntries(
            ResidentRoutineActivityType.SitAtAssignedSeat,
            new[]
            {
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SitAtAssignedSeat, NoonMealStartMinute, NoonMealEndMinute, MealPriority + 5),
                new ResidentScheduleEntryData(ResidentRoutineActivityType.SitAtAssignedSeat, EveningMealStartMinute, EveningMealEndMinute, MealPriority + 5)
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
