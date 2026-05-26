using BepInEx.Logging;
using HarmonyLib;

namespace Wyrdrasil.Registry.Patches;

/// <summary>
/// Obsolete compatibility shim.
///
/// Native piece highlighting is now handled directly by WyrdrasilNativePieceHighlightBridge through
/// Valheim's MaterialMan + WearNTear ResetHighlight lifecycle. Keeping this type avoids stale project
/// files or older bootstraps failing after previous experimental patches, but it no longer installs
/// a Harmony postfix.
/// </summary>
internal static class WyrdrasilWearNTearHighlightPatch
{
    private const string LogPrefix = "[NativePieceHighlightPatch]";
    private static bool _reported;

    public static void Apply(Harmony harmony, ManualLogSource log)
    {
        if (_reported)
        {
            return;
        }

        _reported = true;
        log.LogInfo($"{LogPrefix} Disabled obsolete WearNTear.Highlight postfix; using MaterialMan + WearNTear ResetHighlight lifecycle.");
    }
}
