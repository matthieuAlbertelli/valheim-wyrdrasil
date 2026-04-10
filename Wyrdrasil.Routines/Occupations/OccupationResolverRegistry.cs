using System;
using System.Collections.Generic;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationResolverRegistry
{
    private readonly Dictionary<ResidentRoutineActivityType, IOccupationResolver> _resolvers = new();

    public IEnumerable<IOccupationResolver> Resolvers => _resolvers.Values;

    public void Register(IOccupationResolver resolver)
    {
        if (resolver == null)
        {
            throw new ArgumentNullException(nameof(resolver));
        }

        _resolvers[resolver.ActivityType] = resolver;
    }

    public bool TryGetResolver(ResidentRoutineActivityType activityType, out IOccupationResolver resolver)
    {
        return _resolvers.TryGetValue(activityType, out resolver!);
    }
}
