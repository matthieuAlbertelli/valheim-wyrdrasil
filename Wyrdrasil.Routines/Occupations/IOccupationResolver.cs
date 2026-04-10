using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public interface IOccupationResolver
{
    ResidentRoutineActivityType ActivityType { get; }

    bool TryResolve(RegisteredNpcData resident, out OccupationTarget target);

    void Release(RegisteredNpcData resident);
}
