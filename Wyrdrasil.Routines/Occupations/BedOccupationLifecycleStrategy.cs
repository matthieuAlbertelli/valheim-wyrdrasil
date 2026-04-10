using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class BedOccupationLifecycleStrategy : IOccupationLifecycleStrategy
{
    public string StrategyId => OccupationExecutionProfile.BedStrategyId;

    public OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        return executionService.TryApproachTarget(character, target)
            ? OccupationPhase.Approach
            : OccupationPhase.None;
    }

    public OccupationPhase Continue(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationPhase currentPhase)
    {
        if (executionService.IsOccupyingTarget(character, target))
        {
            return OccupationPhase.Sustain;
        }

        if (executionService.IsNavigationActive(character))
        {
            return OccupationPhase.Approach;
        }

        return OccupationPhase.Engage;
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
    }
}
