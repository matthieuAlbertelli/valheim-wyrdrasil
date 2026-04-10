using System;
using System.Collections.Generic;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationClaimRegistry
{
    private readonly Dictionary<ResidentRoutineActivityType, IOccupationClaimSource> _sources = new();

    public void Register(IOccupationClaimSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _sources[source.ActivityType] = source;
    }

    public bool TryGetClaimSource(ResidentRoutineActivityType activityType, out IOccupationClaimSource source)
    {
        return _sources.TryGetValue(activityType, out source!);
    }
}
