using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationLifecycleStrategy : IOccupationLifecycleStrategy
{
    private const float ApproachArrivalTolerance = 0.10f;
    private const float PosePendingTimeoutSeconds = 1.25f;

    private sealed class PosePendingState
    {
        public float StartedAtTime { get; }
        public bool PoseRequested { get; set; }

        public PosePendingState(float startedAtTime)
        {
            StartedAtTime = startedAtTime;
        }
    }

    private readonly Dictionary<int, PosePendingState> _posePendingStatesByResidentId = new();

    public string StrategyId => OccupationExecutionProfile.CraftStationStrategyId;

    public OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        ClearPendingPoseState(resident.Id);
        GetOrCreateEngagedPoseController(character).Disengage();
        ExitTravelLock(character);

        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }

        if (executionService.IsNearApproachPosition(character, target, target.Plan.NavigationStopDistance + ApproachArrivalTolerance))
        {
            return EngageAtTarget(resident.Id, character, target);
        }

        if (executionService.TryApproachTarget(character, target))
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, $"Begin -> Travel approach={target.Plan.ApproachPosition} engage={target.Plan.EngagePosition}");
            return OccupationPhase.Travel;
        }

        return EngageAtTarget(resident.Id, character, target);
    }

    public OccupationPhase Continue(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationPhase currentPhase)
    {
        switch (currentPhase)
        {
            case OccupationPhase.Travel:
                return ContinueTravel(executionService, resident, character, target);

            case OccupationPhase.EnterPose:
                return ContinueEnterPose(resident, character, target);

            case OccupationPhase.Sustain:
                return OccupationPhase.Sustain;

            default:
                return OccupationPhase.None;
        }
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        ClearPendingPoseState(resident.Id);
        GetOrCreateEngagedPoseController(character).Disengage();
        ExitTravelLock(character);

        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
    }

    private OccupationPhase ContinueTravel(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        var nearApproach = executionService.IsNearApproachPosition(character, target, target.Plan.NavigationStopDistance + ApproachArrivalTolerance);
        var navigationActive = executionService.IsNavigationActive(character);
        if (!nearApproach && navigationActive)
        {
            return OccupationPhase.Travel;
        }

        executionService.ReleaseResidentNavigation(resident, detachIfAttached: false);
        return EngageAtTarget(resident.Id, character, target);
    }

    private OccupationPhase ContinueEnterPose(RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        if (character is not WyrdrasilVikingNpc viking)
        {
            ClearPendingPoseState(resident.Id);
            return OccupationPhase.Sustain;
        }

        if (!_posePendingStatesByResidentId.TryGetValue(resident.Id, out var posePendingState))
        {
            posePendingState = new PosePendingState(Time.time);
            _posePendingStatesByResidentId[resident.Id] = posePendingState;
        }

        if (viking.IsInWorkbenchPose())
        {
            ClearPendingPoseState(resident.Id);
            var targetFacing = GetActorFacingDirection(target.Plan.FacingDirection);
            var visualForward = viking.GetWorkbenchPoseReferenceForward();
            var rootForward = character.transform.forward;
            rootForward.y = 0f;
            if (rootForward.sqrMagnitude > 0.0001f)
            {
                rootForward.Normalize();
            }

            WyrdrasilOccupationDebug.LogCraftStation(character, $"EnterPose -> Sustain confirmed targetFacing={FormatDirection(targetFacing)} visualForward={FormatDirection(visualForward)} rootForward={FormatDirection(rootForward)}");
            return OccupationPhase.Sustain;
        }

        if (!posePendingState.PoseRequested)
        {
            posePendingState.PoseRequested = true;
            _ = viking.TryEnterWorkbenchPose();
            WyrdrasilOccupationDebug.LogCraftStation(character, "EnterPose request sent");
        }

        if (Time.time - posePendingState.StartedAtTime >= PosePendingTimeoutSeconds)
        {
            ClearPendingPoseState(resident.Id);
            WyrdrasilOccupationDebug.LogCraftStation(character, "EnterPose timeout -> Sustain fallback on engaged pose");
            return OccupationPhase.Sustain;
        }

        return OccupationPhase.EnterPose;
    }

    private OccupationPhase EngageAtTarget(int residentId, Character character, OccupationTarget target)
    {
        var facingDirection = GetActorFacingDirection(target.Plan.FacingDirection);
        GetOrCreateEngagedPoseController(character).Engage(target.Plan.EngagePosition, facingDirection);

        if (character is WyrdrasilVikingNpc)
        {
            _posePendingStatesByResidentId[residentId] = new PosePendingState(Time.time);
            WyrdrasilOccupationDebug.LogCraftStation(character, $"Travel -> EnterPose engaged-pose engage={target.Plan.EngagePosition} facing={FormatDirection(facingDirection)}");
            return OccupationPhase.EnterPose;
        }

        WyrdrasilOccupationDebug.LogCraftStation(character, $"Travel -> Sustain engaged-pose engage={target.Plan.EngagePosition} facing={FormatDirection(facingDirection)}");
        return OccupationPhase.Sustain;
    }

    private void ClearPendingPoseState(int residentId)
    {
        _posePendingStatesByResidentId.Remove(residentId);
    }

    private static Vector3 GetActorFacingDirection(Vector3 anchorFacingDirection)
    {
        anchorFacingDirection.y = 0f;
        if (anchorFacingDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return anchorFacingDirection.normalized;
    }

    private static WyrdrasilEngagedPoseController GetOrCreateEngagedPoseController(Character character)
    {
        if (!character.TryGetComponent<WyrdrasilEngagedPoseController>(out var controller))
        {
            controller = character.gameObject.AddComponent<WyrdrasilEngagedPoseController>();
        }

        return controller;
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

    private static void ExitTravelLock(Character character)
    {
        if (character.TryGetComponent<WyrdrasilVikingNpcAI>(out var ai))
        {
            ai.ExitRegistryTravelLock();
        }
    }
}
