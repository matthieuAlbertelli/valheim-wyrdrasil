using HarmonyLib;

namespace Wyrdrasil.Routines;

public static class RoutinesModuleBootstrap
{
    public static void ApplyHarmony(Harmony harmony)
    {
        harmony.PatchAll(typeof(RoutinesModuleBootstrap).Assembly);
    }
}
