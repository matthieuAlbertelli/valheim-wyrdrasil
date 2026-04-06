using UnityEngine;

namespace Wyrdrasil.Registry.Diagnostics;

internal static class WyrdrasilSeatDebug
{
    public static bool Enabled = true;

    public static void Log(Object? source, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var sourceName = source == null ? "null" : source.name;
        Debug.Log($"[Wyrdrasil.Registry][SeatDebug][frame={Time.frameCount}] {sourceName} :: {message}");
    }
}
