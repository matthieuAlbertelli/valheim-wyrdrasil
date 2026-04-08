using HarmonyLib;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Routines.Patches;


[HarmonyPatch(typeof(Chair), nameof(Chair.Interact))]
public static class WyrdrasilChairInteractPatch
{
    private static bool Prefix(Chair __instance, Humanoid human, ref bool __result)
    {
        if (human is not WyrdrasilVikingNpc viking)
        {
            return true;
        }

        WyrdrasilSeatDebug.Log(__instance, $"Chair.Interact prefix for viking={viking.name} attachPoint={(__instance.m_attachPoint != null ? __instance.m_attachPoint.name : "null")}");

        if (__instance.m_attachPoint == null)
        {
            __result = false;
            WyrdrasilSeatDebug.Log(__instance, "Chair.Interact aborted because attachPoint is null");
            return false;
        }

        viking.AttachToChair(__instance);
        __result = true;
        WyrdrasilSeatDebug.Log(__instance, $"Chair.Interact completed attached={viking.IsAttached()}");
        return false;
    }
}
