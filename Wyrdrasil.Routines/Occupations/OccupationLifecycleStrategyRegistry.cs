using System;
using System.Collections.Generic;

namespace Wyrdrasil.Routines.Occupations;

public sealed class OccupationLifecycleStrategyRegistry
{
    private readonly Dictionary<string, IOccupationLifecycleStrategy> _strategies = new();

    public void Register(IOccupationLifecycleStrategy strategy)
    {
        if (strategy == null)
        {
            throw new ArgumentNullException(nameof(strategy));
        }

        _strategies[strategy.StrategyId] = strategy;
    }

    public bool TryGetStrategy(string strategyId, out IOccupationLifecycleStrategy strategy)
    {
        return _strategies.TryGetValue(strategyId, out strategy!);
    }
}
