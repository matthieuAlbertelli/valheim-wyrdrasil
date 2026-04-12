using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Routines.Tool;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationLifecycleStrategy : IOccupationLifecycleStrategy
{
    private const float DockPositionTolerance = 0.08f;
    private const float DockAngleToleranceDegrees = 5f;
    private const float DockMaxDurationSeconds = 1.25f;
    private const float RetryCooldownSeconds = 0.65f;

    private readonly Dictionary<int, float> _retryBlockedUntilByResidentId = new();

    public string StrategyId => OccupationExecutionProfile.CraftStationStrategyId;

    public OccupationPhase Begin(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        if (_retryBlockedUntilByResidentId.TryGetValue(resident.Id, out var retryBlockedUntil) && Time.time < retryBlockedUntil)
        {
            return OccupationPhase.None;
        }

        var travelPosition = target.Plan.ApproachPosition;
        var began = executionService.TryApproachTarget(character, target);
        if (began)
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, $"Begin -> Travel approach={travelPosition} engage={target.Plan.EngagePosition}");
        }

        return began ? OccupationPhase.Travel : OccupationPhase.None;
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

                var dockingController = EnsureDockingController(character);
                if (!dockingController.IsActive && !dockingController.IsDocked)
                {
                    var actorFacing = GetActorFacingDirection(target.Plan.FacingDirection);
                    dockingController.BeginDocking(new DockingRequest(
                        target.Plan.EngagePosition,
                        actorFacing,
                        DockPositionTolerance,
                        DockAngleToleranceDegrees,
                        DockMaxDurationSeconds));

                    WyrdrasilOccupationDebug.LogCraftStation(character, $"AwaitNavigationStop -> Docking engage={target.Plan.EngagePosition} facing={actorFacing}");
                }

                return OccupationPhase.Docking;
            }

            case OccupationPhase.Docking:
            {
                var dockingController = EnsureDockingController(character);
                if (dockingController.IsDocked)
                {
                    WyrdrasilOccupationDebug.LogCraftStation(character, $"Docking -> EnterPose distance={dockingController.CurrentHorizontalDistance:0.000} angle={dockingController.CurrentAngleErrorDegrees:0.0}");
                    return OccupationPhase.EnterPose;
                }

                if (dockingController.HasFailed)
                {
                    _retryBlockedUntilByResidentId[resident.Id] = Time.time + RetryCooldownSeconds;
                    WyrdrasilOccupationDebug.LogCraftStation(character, $"Docking failed distance={dockingController.CurrentHorizontalDistance:0.000} angle={dockingController.CurrentAngleErrorDegrees:0.0} retryAt={_retryBlockedUntilByResidentId[resident.Id]:0.00}");
                    return OccupationPhase.None;
                }

                return OccupationPhase.Docking;
            }

            case OccupationPhase.EnterPose:
                executionService.ReleaseResidentNavigation(resident, detachIfAttached: false);
                if (character is not WyrdrasilVikingNpc viking)
                {
                    return OccupationPhase.None;
                }

                ForceFace(character, GetActorFacingDirection(target.Plan.FacingDirection));
                if (viking.IsInWorkbenchPose() || viking.TryEnterWorkbenchPose())
                {
                    WyrdrasilOccupationDebug.LogCraftStation(character, "EnterPose -> Sustain");
                    return OccupationPhase.Sustain;
                }

                return OccupationPhase.EnterPose;

            case OccupationPhase.Sustain:
                return OccupationPhase.Sustain;

            default:
                return OccupationPhase.None;
        }
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target)
    {
        if (character.TryGetComponent<WyrdrasilOccupationDockingController>(out var dockingController))
        {
            dockingController.CancelDocking();
        }

        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
    }

    private static Vector3 GetActorFacingDirection(Vector3 anchorFacingDirection)
    {
        anchorFacingDirection.y = 0f;
        if (anchorFacingDirection.sqrMagnitude <= 0.0001f)
        {
            return Vector3.forward;
        }

        return -anchorFacingDirection.normalized;
    }

    private static WyrdrasilOccupationDockingController EnsureDockingController(Character character)
    {
        var controller = character.GetComponent<WyrdrasilOccupationDockingController>();
        if (controller == null)
        {
            controller = character.gameObject.AddComponent<WyrdrasilOccupationDockingController>();
        }

        return controller;
    }

    private static void ForceFace(Character character, Vector3 facingDirection)
    {
        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        character.transform.rotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
    }
}
