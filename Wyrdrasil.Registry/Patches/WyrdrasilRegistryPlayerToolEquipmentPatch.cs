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
}
