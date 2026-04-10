using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class StandOccupationLifecycleStrategy : IOccupationLifecycleStrategy
{
    public string StrategyId => OccupationExecutionProfile.StandStrategyId;

    public OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        return executionService.TryApproachTarget(character, target)
            ? OccupationPhase.Approach
            : OccupationPhase.None;
    }

    public OccupationPhase Continue(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationPhase currentPhase)
    {
        return executionService.IsOccupyingTarget(character, target)
            ? OccupationPhase.Sustain
            : OccupationPhase.Approach;
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
    }
}
