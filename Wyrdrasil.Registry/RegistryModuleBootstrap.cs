using HarmonyLib;

namespace Wyrdrasil.Registry;

public static class RegistryModuleBootstrap
{
    public static void ApplyHarmony(Harmony harmony)
    {
        harmony.PatchAll(typeof(RegistryModuleBootstrap).Assembly);
    }
}
