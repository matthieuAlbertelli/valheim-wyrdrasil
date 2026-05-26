using System;
using UnityEngine;

namespace Wyrdrasil.Registry.Diagnostics;

public static class WyrdrasilSeatDebug
{
    public static bool Enabled = false;

    public static bool IsEnabled => Enabled;

    public static void Log(UnityEngine.Object? source, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var sourceName = source == null ? "null" : source.name;
        Debug.Log($"[Wyrdrasil.Registry][SeatDebug][frame={Time.frameCount}] {sourceName} :: {message}");
    }

    public static void LogLazy(UnityEngine.Object? source, Func<string> messageFactory)
    {
        if (!Enabled)
        {
            return;
        }

        Log(source, messageFactory());
    }
}
