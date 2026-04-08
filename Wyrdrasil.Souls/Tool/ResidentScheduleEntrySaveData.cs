using System;

namespace Wyrdrasil.Souls.Tool;


[Serializable]
public sealed class ResidentScheduleEntrySaveData
{
    public ResidentRoutineActivityType ActivityType;
    public int StartMinuteOfDay;
    public int EndMinuteOfDay;
    public int Priority;
}
