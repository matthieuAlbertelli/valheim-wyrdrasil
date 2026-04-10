using System;
using System.Collections.Generic;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationNavigationStrategyRegistry
{
    private readonly Dictionary<string, IOccupationNavigationStrategy> _strategies = new();

    public void Register(IOccupationNavigationStrategy strategy)
    {
        if (strategy == null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        _strategies[strategy.StrategyId] = strategy;
    }

    public bool TryGetStrategy(string strategyId, out IOccupationNavigationStrategy strategy)
    {
        return _strategies.TryGetValue(strategyId, out strategy!);
    }
}
