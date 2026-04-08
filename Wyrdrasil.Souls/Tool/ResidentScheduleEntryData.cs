using System;

namespace Wyrdrasil.Registry.Tool;

public sealed class ResidentScheduleEntryData
{
    public ResidentRoutineActivityType ActivityType { get; }
    public int StartMinuteOfDay { get; }
    public int EndMinuteOfDay { get; }
    public int Priority { get; }

    public ResidentScheduleEntryData(
        ResidentRoutineActivityType activityType,
        int startMinuteOfDay,
        int endMinuteOfDay,
        int priority)
    {
        ActivityType = activityType;
        StartMinuteOfDay = NormalizeMinute(startMinuteOfDay);
        EndMinuteOfDay = NormalizeMinute(endMinuteOfDay);
        Priority = priority;
    }

    public bool ContainsMinute(int minuteOfDay)
    {
        var normalized = NormalizeMinute(minuteOfDay);

        if (StartMinuteOfDay == EndMinuteOfDay)
        {
            return true;
        }

        if (StartMinuteOfDay < EndMinuteOfDay)
        {
            return normalized >= StartMinuteOfDay && normalized < EndMinuteOfDay;
        }

        return normalized >= StartMinuteOfDay || normalized < EndMinuteOfDay;
    }

    private static int NormalizeMinute(int minuteOfDay)
    {
        var normalized = minuteOfDay % 1440;
        return normalized < 0 ? normalized + 1440 : normalized;
    }
}
