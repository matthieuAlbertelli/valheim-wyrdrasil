using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Wyrdrasil.Registry.PlayerTool;

namespace Wyrdrasil.Registry.Patches;

internal static class WyrdrasilRegistryPlayerToolUpdateHoverPatchInstaller
{
    private static bool _applied;
    private static bool _reportedMissingMethod;

    public static void Apply(Harmony harmony, ManualLogSource log)
    {
        if (_applied)
        {
            return;
        }

        var targetMethod = AccessTools.Method(typeof(Player), "UpdateHover", Type.EmptyTypes);
        if (targetMethod == null)
        {
            if (!_reportedMissingMethod)
            {
                _reportedMissingMethod = true;
                log.LogWarning("Registry player tool could not patch Player.UpdateHover: method not found in this Valheim build.");
            }

            return;
        }

        var prefixMethod = typeof(WyrdrasilRegistryPlayerToolUpdateHoverPatchInstaller).GetMethod(
            nameof(Prefix),
            BindingFlags.Static | BindingFlags.NonPublic);

        if (prefixMethod == null)
        {
            log.LogWarning("Registry player tool could not patch Player.UpdateHover: prefix method not found.");
            return;
        }

        harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
        _applied = true;
    }

    private static bool Prefix(Player __instance)
    {
        return !RegistryPlayerToolNativeHoverSuppressor.TrySuppressNativeUpdateHover(__instance);
    }
}
