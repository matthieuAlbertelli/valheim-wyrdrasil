using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationSession
{
    public ResidentRoutineActivityType ActivityType { get; }
    public OccupationTarget Target { get; }
    public OccupationPhase Phase { get; set; }

    public OccupationSession(ResidentRoutineActivityType activityType, OccupationTarget target, OccupationPhase phase)
    {
        ActivityType = activityType;
        Target = target;
        Phase = phase;
    }
}
