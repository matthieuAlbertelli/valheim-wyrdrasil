using UnityEngine;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class AnchoredStandOccupationLifecycleStrategy : IOccupationLifecycleStrategy
{
    private const float ApproachArrivalTolerance = 0.10f;

    public string StrategyId => OccupationExecutionProfile.AnchoredStandLifecycleStrategyId;

    public OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        ReleaseControllers(character);

        if (executionService.IsNearApproachPosition(character, target, target.Plan.NavigationStopDistance + ApproachArrivalTolerance))
        {
            return EngageAtTarget(character, target);
        }

        return executionService.TryApproachTarget(character, target)
            ? OccupationPhase.Travel
            : EngageAtTarget(character, target);
    }

    public OccupationPhase Continue(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationPhase currentPhase)
    {
        switch (currentPhase)
        {
            case OccupationPhase.Travel:
                var nearApproach = executionService.IsNearApproachPosition(character, target, target.Plan.NavigationStopDistance + ApproachArrivalTolerance);
                var navigationActive = executionService.IsNavigationActive(character);
                if (!nearApproach && navigationActive)
                {
                    return OccupationPhase.Travel;
                }

                executionService.ReleaseResidentNavigation(resident, detachIfAttached: false);
                return EngageAtTarget(character, target);

            case OccupationPhase.Sustain:
                return OccupationPhase.Sustain;

            default:
                return OccupationPhase.None;
        }
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        ReleaseControllers(character);
    }

    private static OccupationPhase EngageAtTarget(Character character, OccupationTarget target)
    {
        var facingDirection = NormalizeFacingDirection(target.Plan.FacingDirection);
        GetOrCreateEngagedPoseController(character).Engage(target.Plan.EngagePosition, facingDirection);
        return OccupationPhase.Sustain;
    }

    private static Vector3 NormalizeFacingDirection(Vector3 facingDirection)
    {
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return facingDirection.normalized;
    }

    private static WyrdrasilEngagedPoseController GetOrCreateEngagedPoseController(Character character)
    {
        if (!character.TryGetComponent<WyrdrasilEngagedPoseController>(out var controller))
        {
            controller = character.gameObject.AddComponent<WyrdrasilEngagedPoseController>();
        }

        return controller;
    }

    private static void ReleaseControllers(Character character)
    {
        if (character.TryGetComponent<WyrdrasilEngagedPoseController>(out var controller))
        {
            controller.Disengage();
        }

        WorkbenchPoseRuntime.EnsureExited(character);

        if (character.TryGetComponent<WyrdrasilVikingNpcAI>(out var ai))
        {
            ai.ExitRegistryTravelLock();
        }
    }
}
