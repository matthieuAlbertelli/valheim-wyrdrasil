using HarmonyLib;
using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact))]
public static class WyrdrasilCraftingStationInteractPatch
{
    private static void Prefix(CraftingStation __instance, Humanoid character, bool repeat, bool alt)
    {
        if (character == null || character != Player.m_localPlayer)
        {
            return;
        }

        var delta = __instance.transform.position - character.transform.position;
        var horizontalDistance = new Vector2(delta.x, delta.z).magnitude;
        var directionToStation = delta;
        directionToStation.y = 0f;
        var facingDot = directionToStation.sqrMagnitude > 0.0001f
            ? Vector3.Dot(character.transform.forward.normalized, directionToStation.normalized)
            : 0f;

        WyrdrasilCraftDebug.Log(__instance, $"CraftingStation.Interact prefix player={character.name} repeat={repeat} alt={alt} horizontalDistance={horizontalDistance:0.00} facingDot={facingDot:0.00} stationPos={__instance.transform.position} playerPos={character.transform.position}");
        WyrdrasilCraftDebug.LogAnimatorSnapshot(Player.m_localPlayer, "CraftingStationInteractPrefix");
    }

    private static void Postfix(CraftingStation __instance, Humanoid character, bool repeat, bool alt, bool __result)
    {
        if (character == null || character != Player.m_localPlayer)
        {
            return;
        }

        WyrdrasilCraftDebug.Log(__instance, $"CraftingStation.Interact postfix result={__result} repeat={repeat} alt={alt}");
        WyrdrasilCraftDebug.LogAnimatorSnapshot(Player.m_localPlayer, "CraftingStationInteractPostfix");
    }
}
