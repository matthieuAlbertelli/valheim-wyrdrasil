using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationLifecycleStrategy : IOccupationLifecycleStrategy
{
    public string StrategyId => OccupationExecutionProfile.CraftStationStrategyId;

    public OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        return executionService.TryApproachTarget(character, target)
            ? OccupationPhase.Approach
            : OccupationPhase.None;
    }

    public OccupationPhase Continue(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationPhase currentPhase)
    {
        switch (currentPhase)
        {
            case OccupationPhase.Approach:
            {
                var approachDistance = executionService.GetHorizontalDistance(character, target.Anchor.ApproachPosition);
                var useDistance = executionService.GetHorizontalDistance(character, target.Anchor.UsePosition);
                var isNearApproach = executionService.IsNearApproachPosition(character, target, 0.85f);
                var navigationActive = executionService.IsNavigationActive(character);

                if (isNearApproach || !navigationActive)
                {
                    WyrdrasilSeatDebug.Log(character, $"CraftApproach -> Engage nearApproach={isNearApproach} navigationActive={navigationActive} approachDistance={approachDistance:0.00} useDistance={useDistance:0.00}");
                    return OccupationPhase.Engage;
                }

                if (UnityEngine.Time.frameCount % 60 == 0)
                {
                    WyrdrasilSeatDebug.Log(character, $"CraftApproach waiting nearApproach={isNearApproach} navigationActive={navigationActive} approachDistance={approachDistance:0.00} useDistance={useDistance:0.00}");
                }

                return OccupationPhase.Approach;
            }

            case OccupationPhase.Engage:
                if (character is not Humanoid humanoid)
                {
                    return OccupationPhase.None;
                }

                executionService.ReleaseResidentNavigation(resident, detachIfAttached: false);
                WyrdrasilSeatDebug.Log(character, $"CraftEngage interactable={(target.Execution.Interactable != null)} useDistance={executionService.GetHorizontalDistance(character, target.Anchor.UsePosition):0.00}");

                if (target.Execution.Interactable != null)
                {
                    _ = target.Execution.Interactable.Interact(humanoid, false, false);
                }

                if (character is WyrdrasilVikingNpc viking)
                {
                    _ = viking.TryEnterWorkbenchPose();
                }

                return OccupationPhase.Sustain;

            case OccupationPhase.Sustain:
                return OccupationPhase.Sustain;

            default:
                return OccupationPhase.Engage;
        }
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
    }
}
