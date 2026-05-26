using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Wyrdrasil.Core.Diagnostics;

public static class WyrdrasilProfiler
{
    private sealed class Bucket
    {
        public long CallCount;
        public long TotalTicks;
        public long MaxTicks;
    }

    private static readonly object Sync = new();
    private static readonly Dictionary<string, Bucket> Buckets = new(StringComparer.Ordinal);
    private static readonly double TicksToMilliseconds = 1000.0 / Stopwatch.Frequency;
    private static long _windowStartTimestamp = Stopwatch.GetTimestamp();

    public static bool Enabled { get; set; } = true;

    public static WyrdrasilProfilerSample Sample(string name)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(name))
        {
            return new WyrdrasilProfilerSample(null);
        }

        return new WyrdrasilProfilerSample(name);
    }

    internal static void Record(string name, long elapsedTicks)
    {
        if (!Enabled || elapsedTicks < 0L)
        {
            return;
        }

        lock (Sync)
        {
            if (!Buckets.TryGetValue(name, out var bucket))
            {
                bucket = new Bucket();
                Buckets.Add(name, bucket);
            }

            bucket.CallCount++;
            bucket.TotalTicks += elapsedTicks;
            if (elapsedTicks > bucket.MaxTicks)
            {
                bucket.MaxTicks = elapsedTicks;
            }
        }
    }

    public static WyrdrasilProfilerReport TakeReportAndReset(string scopePrefix = "")
    {
        lock (Sync)
        {
            var now = Stopwatch.GetTimestamp();
            var windowSeconds = Math.Max(0.001, (now - _windowStartTimestamp) / (double)Stopwatch.Frequency);
            _windowStartTimestamp = now;

            var entries = Buckets
                .Where(pair => string.IsNullOrEmpty(scopePrefix) || pair.Key.StartsWith(scopePrefix, StringComparison.Ordinal))
                .Select(pair => new WyrdrasilProfilerReportEntry(
                    pair.Key,
                    pair.Value.CallCount,
                    pair.Value.TotalTicks * TicksToMilliseconds,
                    pair.Value.MaxTicks * TicksToMilliseconds))
                .OrderByDescending(entry => entry.MaxMilliseconds)
                .ThenByDescending(entry => entry.TotalMilliseconds)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .ToList();

            if (string.IsNullOrEmpty(scopePrefix))
            {
                Buckets.Clear();
            }
            else
            {
                var keysToRemove = Buckets.Keys
                    .Where(key => key.StartsWith(scopePrefix, StringComparison.Ordinal))
                    .ToList();
                foreach (var key in keysToRemove)
                {
                    Buckets.Remove(key);
                }
            }

            return new WyrdrasilProfilerReport(scopePrefix, windowSeconds, entries);
        }
    }
}
