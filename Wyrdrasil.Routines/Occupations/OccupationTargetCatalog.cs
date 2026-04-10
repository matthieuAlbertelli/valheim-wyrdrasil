using System;
using System.Collections.Generic;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationTargetCatalog
{
    private readonly Dictionary<OccupationTargetKind, IOccupationTargetSource> _sources = new();

    public void Register(IOccupationTargetSource source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _sources[source.TargetKind] = source;
    }

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (_sources.TryGetValue(targetRef.TargetKind, out var source) &&
            source.TryResolve(targetRef, out target))
        {
            return true;
        }

        target = null!;
        return false;
    }
}
