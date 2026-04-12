using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationSustainStrategy : IOccupationSustainStrategy
{
    public string StrategyId => OccupationExecutionProfile.CraftStationStrategyId;
    public float TickIntervalSeconds => 0.25f;

    public OccupationSustainResult Sustain(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        var navigationActive = executionService.IsNavigationActive(character);
        if (navigationActive)
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, $"Sustain abort navigationActive={navigationActive}");
            return OccupationSustainResult.Abort;
        }

        if (!character.TryGetComponent<WyrdrasilEngagedPoseController>(out var controller) || !controller.IsEngaged)
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, "Sustain abort engagedPoseControllerMissing");
            return OccupationSustainResult.Abort;
        }

        var targetFacing = target.Plan.FacingDirection;
        targetFacing.y = 0f;
        if (targetFacing.sqrMagnitude > 0.0001f)
        {
            targetFacing.Normalize();
        }

        var visualForward = character is WyrdrasilVikingNpc viking
            ? viking.GetWorkbenchPoseReferenceForward()
            : character.transform.forward;
        var rootForward = character.transform.forward;
        rootForward.y = 0f;
        if (rootForward.sqrMagnitude > 0.0001f)
        {
            rootForward.Normalize();
        }

        var inWorkbenchPose = true;
        if (character is WyrdrasilVikingNpc craftViking)
        {
            inWorkbenchPose = craftViking.IsInWorkbenchPose();
            if (!inWorkbenchPose)
            {
                _ = craftViking.TryEnterWorkbenchPose();
            }
        }

        WyrdrasilOccupationDebug.LogCraftStation(character, $"Sustain engaged-pose engage={target.Plan.EngagePosition} targetFacing={FormatDirection(targetFacing)} visualForward={FormatDirection(visualForward)} rootForward={FormatDirection(rootForward)} inWorkbenchPose={inWorkbenchPose}");
        return OccupationSustainResult.Continue;
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
    }

    private static string FormatDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return "(0.00,0.00,0.00)";
        }

        direction.Normalize();
        return $"({direction.x:0.00},{direction.y:0.00},{direction.z:0.00})";
    }
}
