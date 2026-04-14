using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Routines.Occupations;

public static class WorkbenchPoseRuntime
{
    public static void EnsureEntered(Character character)
    {
        if (character is WyrdrasilVikingNpc viking && !viking.IsInWorkbenchPose())
        {
            _ = viking.TryEnterWorkbenchPose();
        }
    }

    public static void EnsureExited(Character character)
    {
        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
    }
}
