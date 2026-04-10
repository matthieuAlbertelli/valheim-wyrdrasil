using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public interface IOccupationLifecycleStrategy
{
    string StrategyId { get; }

    OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target);

    OccupationPhase Continue(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationPhase currentPhase);

    void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target);
}
