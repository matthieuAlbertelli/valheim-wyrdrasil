using System.Linq;
using UnityEngine;

namespace Wyrdrasil.Registry.Diagnostics;

public static class WyrdrasilCraftDebug
{
    public static bool Enabled = true;

    public static void Log(Object? source, string message)
    {
        if (!Enabled)
        {
            return;
        }

        var sourceName = source == null ? "null" : source.name;
        Debug.Log($"[Wyrdrasil.Registry][CraftDebug][frame={Time.frameCount}] {sourceName} :: {message}");
    }

    public static void LogAnimatorSnapshot(Player? player, string reason, bool detailed = false)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            Log(player, $"{reason} | animator=null");
            return;
        }

        var state = animator.GetCurrentAnimatorStateInfo(0);
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        var clipNames = clips.Length == 0
            ? "<none>"
            : string.Join(", ", clips.Select(clip => clip.clip == null ? "null" : clip.clip.name).Distinct().ToArray());

        var message = $"{reason} | stateHash={state.fullPathHash} shortHash={state.shortNameHash} normalizedTime={state.normalizedTime:0.00} speed={state.speed:0.00} clips=[{clipNames}] attached={player.IsAttached()}";
        if (detailed)
        {
            var isInTransition = animator.IsInTransition(0);
            var nextState = isInTransition ? animator.GetNextAnimatorStateInfo(0) : default;
            message += $" transition={isInTransition}";
            if (isInTransition)
            {
                message += $" nextFullHash={nextState.fullPathHash} nextShortHash={nextState.shortNameHash} nextNormalizedTime={nextState.normalizedTime:0.00}";
            }

            message += $" params=[{DescribeAnimatorParameters(animator)}]";
        }

        Log(player, message);
    }

    private static string DescribeAnimatorParameters(Animator animator)
    {
        return string.Join(", ", animator.parameters.Select(parameter =>
        {
            return parameter.type switch
            {
                AnimatorControllerParameterType.Bool => $"{parameter.name}:{animator.GetBool(parameter.nameHash)}",
                AnimatorControllerParameterType.Int => $"{parameter.name}:{animator.GetInteger(parameter.nameHash)}",
                AnimatorControllerParameterType.Float => $"{parameter.name}:{animator.GetFloat(parameter.nameHash):0.00}",
                AnimatorControllerParameterType.Trigger => $"{parameter.name}:<trigger>",
                _ => $"{parameter.name}:?"
            };
        }));
    }
}
