using HarmonyLib;
using UnityEngine;
using Wyrdrasil.Registry.PlayerTool;

namespace Wyrdrasil.Registry.Patches;

[HarmonyPatch]
internal static class WyrdrasilRegistryPlayerToolEquipmentPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    private static void RepairRegistryToolBeforeEquip(ItemDrop.ItemData item)
    {
        RegistryPlayerToolItemDataRepair.RepairItemData(item);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), "SetupVisEquipment")]
    private static void RepairRegistryToolBeforeVisualSetup(Humanoid __instance)
    {
        RegistryPlayerToolItemDataRepair.RepairHumanoidEquipment(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.DropItem))]
    private static void RepairRegistryToolBeforeDrop(ItemDrop.ItemData item)
    {
        RegistryPlayerToolItemDataRepair.RepairItemData(item);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Pickup))]
    private static void CanonicalizeRegistryToolBeforePickup(ItemDrop __instance, out bool __state)
    {
        __state = RegistryPlayerToolItemDataRepair.LooksLikeRegistryTool(__instance);
        RegistryPlayerToolItemDataRepair.RepairItemDrop(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Pickup))]
    private static void CanonicalizeRegistryToolAfterPickup()
    {
        // ItemDrop.Pickup can clone or rewrite ItemData while moving the world item into
        // the inventory. A silent post-pickup pass keeps freshly spawned registry tools
        // canonical before the periodic legacy repair pass can observe them.
        //
        // This scans only the local player's inventory/equipment and RepairPlayerItems is
        // a no-op for non-registry items, so running it after any pickup is simpler and
        // more robust than depending on fragile pre-pickup ItemData state.
        RegistryPlayerToolItemDataRepair.RepairPlayerItems(Player.m_localPlayer);
    }
}
