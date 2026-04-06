using HarmonyLib;
using Wyrdrasil.Registry.Components;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch(typeof(Chair), nameof(Chair.Interact))]
public static class WyrdrasilChairInteractPatch
{
    private static bool Prefix(Chair __instance, Humanoid human, ref bool __result)
    {
        if (human is not WyrdrasilVikingNpc viking)
        {
            return true;
        }

        if (__instance.m_attachPoint == null)
        {
            __result = false;
            return false;
        }

        viking.AttachToChair(__instance);
        __result = true;
        return false;
    }
}
