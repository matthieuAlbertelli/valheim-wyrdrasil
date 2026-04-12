using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
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
        var isNearWorkbench = executionService.IsNearUsePosition(character, target, 1.35f) ||
                              executionService.IsNearApproachPosition(character, target, 0.95f);
        var navigationActive = executionService.IsNavigationActive(character);

        if (!isNearWorkbench || navigationActive)
        {
            WyrdrasilSeatDebug.Log(character, $"CraftSustain abort isNearWorkbench={isNearWorkbench} navigationActive={navigationActive} approachDistance={executionService.GetHorizontalDistance(character, target.Anchor.ApproachPosition):0.00} useDistance={executionService.GetHorizontalDistance(character, target.Anchor.UsePosition):0.00}");
            return OccupationSustainResult.Abort;
        }

        var isInWorkbenchPose = character is WyrdrasilVikingNpc viking && viking.IsInWorkbenchPose();
        if (!isInWorkbenchPose)
        {
            ForceFaceUseDirection(character, target.Anchor.FacingDirection);
            if (character is WyrdrasilVikingNpc waitingViking)
            {
                _ = waitingViking.TryEnterWorkbenchPose();
            }
        }

        return OccupationSustainResult.Continue;
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
    }

    private static void ForceFaceUseDirection(Character character, Vector3 useForward)
    {
        useForward.y = 0f;
        if (useForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var targetRotation = Quaternion.LookRotation(useForward.normalized, Vector3.up);
        character.transform.rotation = targetRotation;
    }
}
