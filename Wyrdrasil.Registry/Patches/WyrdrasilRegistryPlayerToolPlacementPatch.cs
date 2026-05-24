using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Wyrdrasil.Registry.PlayerTool;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch]
internal static class WyrdrasilRegistryPlayerToolPlacementPatch
{
    private static MethodBase TargetMethod()
    {
        var method = typeof(Player)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, "UpdatePlacement", StringComparison.Ordinal));

        if (method == null)
        {
            throw new MissingMethodException(typeof(Player).FullName, "UpdatePlacement");
        }

        return method;
    }

    private static bool Prefix(
        Player __instance,
        ref RegistryPlayerToolNativeHoverSuppressor.BuildRaycastSuppressionState __state)
    {
        RegistryPlayerToolNativeHoverSuppressor.BeginBuildRaycastSuppression(__instance, out __state);
        return !RegistryPlayerToolPlacementInterceptor.TryConsumePlacementAction(__instance);
    }

    private static void Postfix(
        Player __instance,
        RegistryPlayerToolNativeHoverSuppressor.BuildRaycastSuppressionState __state)
    {
        RegistryPlayerToolNativeHoverSuppressor.EndBuildRaycastSuppression(__instance, __state);
        RegistryPlayerToolNativeHoverSuppressor.SuppressNativeHoverBehindTargetCharacter(__instance);
    }
}
