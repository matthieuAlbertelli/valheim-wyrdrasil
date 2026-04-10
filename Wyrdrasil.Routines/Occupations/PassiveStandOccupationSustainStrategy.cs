using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class PassiveStandOccupationSustainStrategy : IOccupationSustainStrategy
{
    public string StrategyId => OccupationExecutionProfile.StandStrategyId;
    public float TickIntervalSeconds => 0.5f;

    public OccupationSustainResult Sustain(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        return executionService.IsOccupyingTarget(character, target)
            ? OccupationSustainResult.Continue
            : OccupationSustainResult.Abort;
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
    }
}
