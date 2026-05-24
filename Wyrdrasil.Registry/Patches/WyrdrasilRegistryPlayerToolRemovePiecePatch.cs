using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Wyrdrasil.Registry.PlayerTool;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch]
internal static class WyrdrasilRegistryPlayerToolRemovePiecePatch
{
    private static MethodBase TargetMethod()
    {
        var method = typeof(Player)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, "RemovePiece", StringComparison.Ordinal));

        if (method == null)
        {
            throw new MissingMethodException(typeof(Player).FullName, "RemovePiece");
        }

        return method;
    }

    private static bool Prefix(Player __instance)
    {
        return !RegistryPlayerToolPlacementInterceptor.TryConsumeNativeRemoveAction(__instance);
    }
}
