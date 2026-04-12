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
    private const float WorkPoseCaptureRadius = 1.20f;
    private const float RetryCooldownSeconds = 0.65f;
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

    private readonly Dictionary<int, float> _retryBlockedUntilByResidentId = new();
    private readonly Dictionary<int, PosePendingState> _posePendingStatesByResidentId = new();

    public string StrategyId => OccupationExecutionProfile.CraftStationStrategyId;

    public OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        ClearPendingPoseState(resident.Id);
        ExitTravelLock(character);

        if (_retryBlockedUntilByResidentId.TryGetValue(resident.Id, out var retryBlockedUntil) && Time.time < retryBlockedUntil)
        {
            return OccupationPhase.None;
        }

        var facingDirection = GetActorFacingDirection(target.Plan.FacingDirection);
        GetOrCreateEngagedPoseController(character).Engage(target.Plan.EngagePosition, facingDirection);

        if (character is WyrdrasilVikingNpc)
        {
            _posePendingStatesByResidentId[resident.Id] = new PosePendingState(Time.time);
            WyrdrasilOccupationDebug.LogCraftStation(character, $"Begin -> EnterPose engaged-pose engage={target.Plan.EngagePosition} facing={FormatDirection(facingDirection)}");
            return OccupationPhase.EnterPose;
        }

        WyrdrasilOccupationDebug.LogCraftStation(character, $"Begin -> Sustain engaged-pose engage={target.Plan.EngagePosition} facing={FormatDirection(facingDirection)}");
        return OccupationPhase.Sustain;
    }

    public OccupationPhase Continue(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationPhase currentPhase)
    {
        switch (currentPhase)
        {
            case OccupationPhase.Travel:
            {
                var navigationActive = executionService.IsNavigationActive(character);
                var nearApproach = executionService.IsNearApproachPosition(character, target, target.Plan.NavigationStopDistance + 0.10f);
                if (nearApproach || !navigationActive)
                {
                    executionService.ReleaseResidentNavigation(resident, detachIfAttached: false);
                    WyrdrasilOccupationDebug.LogCraftStation(character, $"Travel -> AwaitNavigationStop nearApproach={nearApproach} navigationActive={navigationActive} approachDistance={executionService.GetHorizontalDistance(character, target.Plan.ApproachPosition):0.00}");
                    return OccupationPhase.AwaitNavigationStop;
                }

                return OccupationPhase.Travel;
            }

            case OccupationPhase.AwaitNavigationStop:
            {
                executionService.ReleaseResidentNavigation(resident, detachIfAttached: false);
                if (executionService.IsNavigationActive(character))
                {
                    return OccupationPhase.AwaitNavigationStop;
                }

                var captureDistance = executionService.GetHorizontalDistance(character, target.Plan.EngagePosition);
                if (captureDistance > WorkPoseCaptureRadius)
                {
                    ClearPendingPoseState(resident.Id);
                    _retryBlockedUntilByResidentId[resident.Id] = Time.time + RetryCooldownSeconds;
                    WyrdrasilOccupationDebug.LogCraftStation(character, $"Work slot capture failed distance={captureDistance:0.000} retryAt={_retryBlockedUntilByResidentId[resident.Id]:0.00}");
                    return OccupationPhase.None;
                }

                SnapToWorkPose(character, target.Plan.EngagePosition, GetActorFacingDirection(target.Plan.FacingDirection));
                _posePendingStatesByResidentId[resident.Id] = new PosePendingState(Time.time);
                WyrdrasilOccupationDebug.LogCraftStation(character, $"AwaitNavigationStop -> EnterPose snapped engage={target.Plan.EngagePosition} captureDistance={captureDistance:0.000}");
                return OccupationPhase.EnterPose;
            }

            case OccupationPhase.EnterPose:
            {
                if (character is not WyrdrasilVikingNpc viking)
                {
                    ClearPendingPoseState(resident.Id);
                    return OccupationPhase.None;
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
                    _retryBlockedUntilByResidentId[resident.Id] = Time.time + RetryCooldownSeconds;
                    WyrdrasilOccupationDebug.LogCraftStation(character, $"EnterPose timeout retryAt={_retryBlockedUntilByResidentId[resident.Id]:0.00}");
                    return OccupationPhase.None;
                }

                return OccupationPhase.EnterPose;
            }

            case OccupationPhase.Sustain:
                return OccupationPhase.Sustain;

            default:
                return OccupationPhase.None;
        }
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        ClearPendingPoseState(resident.Id);
        ExitTravelLock(character);
        GetOrCreateEngagedPoseController(character).Disengage();

        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
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

    private static void SnapToWorkPose(Character character, Vector3 engagePosition, Vector3 actorFacingDirection)
    {
        if (character.TryGetComponent<WyrdrasilRouteTraversalController>(out var routeTraversalController))
        {
            routeTraversalController.ReleaseControl();
        }

        if (character.TryGetComponent<WyrdrasilVikingNpcAI>(out var ai))
        {
            ai.EnterRegistryTravelLock();
        }

        character.transform.position = engagePosition;

        ForceFace(character, actorFacingDirection);

        if (character.TryGetComponent<Rigidbody>(out var rigidbody))
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private static void ForceFace(Character character, Vector3 facingDirection)
    {
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var desiredDirection = facingDirection.normalized;

        if (character is WyrdrasilVikingNpc viking)
        {
            var currentVisualForward = viking.GetWorkbenchPoseReferenceForward();
            var signedAngle = Vector3.SignedAngle(currentVisualForward, desiredDirection, Vector3.up);
            character.transform.rotation = Quaternion.AngleAxis(signedAngle, Vector3.up) * character.transform.rotation;
            return;
        }

        character.transform.rotation = Quaternion.LookRotation(desiredDirection, Vector3.up);
    }
}
