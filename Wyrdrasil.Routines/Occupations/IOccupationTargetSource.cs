using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

public interface IOccupationTargetSource
{
    OccupationTargetKind TargetKind { get; }

    bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target);
}
