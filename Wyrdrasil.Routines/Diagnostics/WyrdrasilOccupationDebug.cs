using UnityEngine;

namespace Wyrdrasil.Registry.Diagnostics;

public static class WyrdrasilOccupationDebug
{
    public static bool Enabled = true;

    public static void LogCraftStation(Object? source, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var sourceName = source == null ? "null" : source.name;
        Debug.Log($"[Wyrdrasil.Routines][CraftStation][frame={Time.frameCount}] {sourceName} :: {message}");
    }
}
