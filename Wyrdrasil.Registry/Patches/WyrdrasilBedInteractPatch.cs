using HarmonyLib;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Diagnostics;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch(typeof(Bed), nameof(Bed.Interact))]
public static class WyrdrasilBedInteractPatch
{
    private static bool Prefix(Bed __instance, Humanoid human, ref bool __result)
    {
        if (human is not WyrdrasilVikingNpc viking)
        {
            return true;
        }

        var attachPoint = ResolveAttachPoint(__instance);
        if (attachPoint == null)
        {
            __result = false;
            WyrdrasilSeatDebug.Log(__instance, "Bed.Interact aborted because no attach point could be resolved");
            return false;
        }

        viking.AttachToBed(__instance, attachPoint);
        __result = true;
        return false;
    }

    private static Transform? ResolveAttachPoint(Bed bed)
    {
        var type = bed.GetType();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

        var attachPointField = type.GetField("m_attachPoint", flags);
        if (attachPointField?.GetValue(bed) is Transform attachPoint)
        {
            return attachPoint;
        }

        var spawnPointField = type.GetField("m_spawnPoint", flags);
        if (spawnPointField?.GetValue(bed) is Transform spawnPoint)
        {
            return spawnPoint;
        }

        return bed.transform;
    }
}
