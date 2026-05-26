using System;
using System.Diagnostics;

namespace Wyrdrasil.Core.Diagnostics;

public readonly struct WyrdrasilProfilerSample : IDisposable
{
    private readonly string? _name;
    private readonly long _startTimestamp;

    internal WyrdrasilProfilerSample(string? name)
    {
        _name = name;
        _startTimestamp = name == null ? 0L : Stopwatch.GetTimestamp();
    }

    public void Dispose()
    {
        if (_name == null)
        {
            return;
        }

        WyrdrasilProfiler.Record(_name, Stopwatch.GetTimestamp() - _startTimestamp);
    }
}
