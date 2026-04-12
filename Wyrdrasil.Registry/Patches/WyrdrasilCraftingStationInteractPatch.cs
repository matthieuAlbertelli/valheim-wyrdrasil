using HarmonyLib;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Diagnostics;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact))]
public static class WyrdrasilCraftingStationInteractPatch
{
    private static void Prefix(CraftingStation __instance, Humanoid user, bool repeat, bool alt)
    {
        if (user == null || user != Player.m_localPlayer)
        {
            return;
        }

        var delta = __instance.transform.position - user.transform.position;
        var horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
        var directionToStation = delta;
        directionToStation.y = 0f;

        var facingDot = directionToStation.sqrMagnitude > 0.0001f
            ? Vector3.Dot(user.transform.forward.normalized, directionToStation.normalized)
            : 0f;

        WyrdrasilCraftDebug.Log(
            __instance,
            $"CraftingStation.Interact prefix player={user.name} repeat={repeat} alt={alt} horizontalDistance={horizontalDistance:0.00} facingDot={facingDot:0.00} stationPos={__instance.transform.position} playerPos={user.transform.position}");

        WyrdrasilCraftDebug.LogAnimatorSnapshot(Player.m_localPlayer, "CraftingStationInteractPrefix", detailed: true);
        WyrdrasilPlayerCraftDebugMonitor.EnsureAttached(Player.m_localPlayer)?.BeginTrace("CraftingStationInteract", 120);
    }

    private static void Postfix(CraftingStation __instance, Humanoid user, bool repeat, bool alt, bool __result)
    {
        if (user == null || user != Player.m_localPlayer)
        {
            return;
        }

        WyrdrasilCraftDebug.Log(__instance, $"CraftingStation.Interact postfix result={__result} repeat={repeat} alt={alt}");
        WyrdrasilCraftDebug.LogAnimatorSnapshot(Player.m_localPlayer, "CraftingStationInteractPostfix", detailed: true);
    }
}