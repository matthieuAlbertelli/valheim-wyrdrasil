using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wyrdrasil.Core.Diagnostics;

public sealed class WyrdrasilProfilerReport
{
    public WyrdrasilProfilerReport(string scopePrefix, double windowSeconds, IReadOnlyList<WyrdrasilProfilerReportEntry> entries)
    {
        ScopePrefix = scopePrefix;
        WindowSeconds = windowSeconds;
        Entries = entries;
    }

    public string ScopePrefix { get; }
    public double WindowSeconds { get; }
    public IReadOnlyList<WyrdrasilProfilerReportEntry> Entries { get; }
    public bool HasEntries => Entries.Count > 0;

    public string ToMultilineString(int maxEntries = 12)
    {
        if (Entries.Count == 0)
        {
            return "<no samples>";
        }

        var builder = new StringBuilder();
        foreach (var entry in Entries.Take(Math.Max(1, maxEntries)))
        {
            builder.Append("  ");
            builder.Append(entry.Name);
            builder.Append(" calls=");
            builder.Append(entry.CallCount);
            builder.Append(" avg=");
            builder.Append(entry.AverageMilliseconds.ToString("0.###"));
            builder.Append("ms max=");
            builder.Append(entry.MaxMilliseconds.ToString("0.###"));
            builder.Append("ms total=");
            builder.Append(entry.TotalMilliseconds.ToString("0.###"));
            builder.AppendLine("ms");
        }

        return builder.ToString().TrimEnd();
    }
}

public sealed class WyrdrasilProfilerReportEntry
{
    public WyrdrasilProfilerReportEntry(
        string name,
        long callCount,
        double totalMilliseconds,
        double maxMilliseconds)
    {
        Name = name;
        CallCount = callCount;
        TotalMilliseconds = totalMilliseconds;
        MaxMilliseconds = maxMilliseconds;
    }

    public string Name { get; }
    public long CallCount { get; }
    public double TotalMilliseconds { get; }
    public double MaxMilliseconds { get; }
    public double AverageMilliseconds => CallCount <= 0 ? 0.0 : TotalMilliseconds / CallCount;
}
