using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public interface IOccupationSustainStrategy
{
    string StrategyId { get; }
    float TickIntervalSeconds { get; }

    OccupationSustainResult Sustain(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session);

    void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session);
}
