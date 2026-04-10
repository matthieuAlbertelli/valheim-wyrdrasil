using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationSession
{
    public ResidentRoutineActivityType ActivityType { get; }
    public OccupationTarget Target { get; }
    public OccupationPhase Phase { get; private set; }
    public float StartedAtTime { get; }
    public float PhaseEnteredAtTime { get; private set; }
    public float LastSustainTickTime { get; private set; }
    public int SustainTickCount { get; private set; }

    public OccupationSession(ResidentRoutineActivityType activityType, OccupationTarget target, OccupationPhase phase, float startedAtTime)
    {
        ActivityType = activityType;
        Target = target;
        Phase = phase;
        StartedAtTime = startedAtTime;
        PhaseEnteredAtTime = startedAtTime;
        LastSustainTickTime = phase == OccupationPhase.Sustain ? startedAtTime : float.NegativeInfinity;
    }

    public void SetPhase(OccupationPhase phase, float currentTime)
    {
        if (Phase == phase)
        {
            return;
        }

        Phase = phase;
        PhaseEnteredAtTime = currentTime;

        if (phase == OccupationPhase.Sustain)
        {
            LastSustainTickTime = currentTime;
        }
    }

    public bool ShouldRunSustainTick(float intervalSeconds, float currentTime)
    {
        return intervalSeconds <= 0f || currentTime - LastSustainTickTime >= intervalSeconds;
    }

    public void RegisterSustainTick(float currentTime)
    {
        LastSustainTickTime = currentTime;
        SustainTickCount++;
    }
}
