using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public interface IOccupationClaimSource
{
    ResidentRoutineActivityType ActivityType { get; }

    bool TryClaim(RegisteredNpcData resident, out OccupationTarget target);

    void Release(RegisteredNpcData resident);
}
