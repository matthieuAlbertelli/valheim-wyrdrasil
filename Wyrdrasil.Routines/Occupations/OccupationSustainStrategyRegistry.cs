using System;
using System.Collections.Generic;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationSustainStrategyRegistry
{
    private readonly Dictionary<string, IOccupationSustainStrategy> _strategies = new();

    public void Register(IOccupationSustainStrategy strategy)
    {
        if (strategy == null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        _strategies[strategy.StrategyId] = strategy;
    }

    public bool TryGetStrategy(string strategyId, out IOccupationSustainStrategy strategy)
    {
        return _strategies.TryGetValue(strategyId, out strategy!);
    }
}
