using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Souls.Components;

namespace Wyrdrasil.Routines.Services;

public sealed class NpcNavigationService
{
    private readonly ManualLogSource _log;

    public NpcNavigationService(ManualLogSource log)
    {
        _log = log;
    }

    public void NavigateAlongRoute(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        switch (target.UseMode)
        {
            case OccupationUseMode.Sit:
                NavigateAlongRouteToSeat(character, routePoints, target);
                return;

            case OccupationUseMode.Lie:
                NavigateAlongRouteToBed(character, routePoints, target);
                return;

            case OccupationUseMode.Stand:
            default:
                NavigateAlongRouteToPosition(character, routePoints, target.Anchor.UsePosition, 0.3f, target.Anchor.FacingDirection);
                return;
        }
    }

    public void NavigateDirectly(Character character, OccupationTarget target)
    {
        switch (target.UseMode)
        {
            case OccupationUseMode.Sit:
                NavigateDirectlyToSeat(character, target);
                return;

            case OccupationUseMode.Lie:
                NavigateDirectlyToBed(character, target);
                return;

            case OccupationUseMode.Stand:
            default:
                NavigateDirectlyToPosition(character, target.Anchor.UsePosition, 0.3f, target.Anchor.FacingDirection);
                return;
        }
    }

    public void NavigateAlongRouteToPosition(
        Character character,
        IReadOnlyList<UnityEngine.Vector3> routePoints,
        UnityEngine.Vector3 destination,
        float stopDistance = 0.3f,
        UnityEngine.Vector3? facingDirection = null)
    {
        var resolvedFacingDirection = facingDirection.HasValue && facingDirection.Value.sqrMagnitude > 0.0001f
            ? facingDirection.Value
            : destination - character.transform.position;

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToPosition(routePoints, destination, stopDistance, resolvedFacingDirection);
            _log.LogInfo($"Assigned registry viking configured for waypoint route navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForRoute(routePoints, destination, 1.15f, stopDistance);
        _log.LogInfo($"Assigned resident configured for waypoint route navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToPosition(
        Character character,
        UnityEngine.Vector3 destination,
        float stopDistance = 0.3f,
        UnityEngine.Vector3? facingDirection = null)
    {
        var resolvedFacingDirection = facingDirection.HasValue && facingDirection.Value.sqrMagnitude > 0.0001f
            ? facingDirection.Value
            : destination - character.transform.position;

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToPosition(System.Array.Empty<UnityEngine.Vector3>(), destination, stopDistance, resolvedFacingDirection);
            _log.LogInfo("Assigned registry viking configured for deterministic direct movement fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectMovement(destination, 1.15f, stopDistance);
        _log.LogInfo("Assigned resident configured for direct movement fallback.");
    }

    public bool IsNavigationActive(Character character)
    {
        var routeController = character.GetComponent<WyrdrasilRouteTraversalController>();
        if (routeController != null && routeController.IsTraversalActive)
        {
            return true;
        }

        var slotController = character.GetComponent<WyrdrasilAssignedSlotController>();
        return slotController != null && slotController.IsControlActive;
    }

    public void ReleaseOccupation(Character character, bool detachIfAttached = true)
    {
        ReleaseLegacyControllers(character, detachIfAttached);
    }

    private void NavigateAlongRouteToSeat(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateAlongRouteToSeat target={target.Reference} routeCount={routePoints.Count}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToSeat(routePoints, target.Anchor.ApproachPosition, target.Anchor.UsePosition, target.Anchor.FacingDirection, target.ChairComponent);
            _log.LogInfo($"Assigned registry viking configured for designated seat navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForSeatRoute(
            routePoints,
            target.Anchor.ApproachPosition,
            target.Anchor.UsePosition,
            target.Anchor.FacingDirection,
            target.ChairComponent,
            1.15f,
            0.25f);
        _log.LogInfo($"Assigned resident configured for designated seat navigation with {routePoints.Count} waypoint(s).");
    }

    private void NavigateDirectlyToSeat(Character character, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateDirectlyToSeat target={target.Reference} approach={target.Anchor.ApproachPosition} seat={target.Anchor.UsePosition}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            vikingAi.StartSeatApproach(target.Anchor.ApproachPosition, target.Anchor.UsePosition, target.Anchor.FacingDirection, target.ChairComponent, false);
            _log.LogInfo("Assigned registry viking configured for direct designated seat fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectSeatMovement(
            target.Anchor.ApproachPosition,
            target.Anchor.UsePosition,
            target.Anchor.FacingDirection,
            target.ChairComponent,
            1.15f,
            0.25f);
        _log.LogInfo("Assigned resident configured for direct designated seat fallback.");
    }

    private void NavigateAlongRouteToBed(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateAlongRouteToBed target={target.Reference} routeCount={routePoints.Count}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToBed(routePoints, target.Anchor.ApproachPosition, target.Anchor.UsePosition, target.Anchor.FacingDirection, target.BedComponent, target.AttachPoint);
            _log.LogInfo($"Assigned registry viking configured for bed navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForBedRoute(
            routePoints,
            target.Anchor.ApproachPosition,
            target.Anchor.UsePosition,
            target.Anchor.FacingDirection,
            target.BedComponent,
            target.AttachPoint,
            1.15f,
            0.25f);
        _log.LogInfo($"Assigned resident configured for bed navigation with {routePoints.Count} waypoint(s).");
    }

    private void NavigateDirectlyToBed(Character character, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateDirectlyToBed target={target.Reference} approach={target.Anchor.ApproachPosition} sleep={target.Anchor.UsePosition}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            vikingAi.StartBedApproach(target.Anchor.ApproachPosition, target.Anchor.UsePosition, target.Anchor.FacingDirection, target.BedComponent, target.AttachPoint, false);
            _log.LogInfo("Assigned registry viking configured for direct bed fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectBedMovement(
            target.Anchor.ApproachPosition,
            target.Anchor.UsePosition,
            target.Anchor.FacingDirection,
            target.BedComponent,
            target.AttachPoint,
            1.15f,
            0.25f);
        _log.LogInfo("Assigned resident configured for direct bed fallback.");
    }

    private static void ReleaseLegacyControllers(Character character, bool detachIfAttached)
    {
        if (detachIfAttached)
        {
            if (character is WyrdrasilVikingNpc viking && viking.IsAttached())
            {
                viking.ForceDetachFromCurrentAnchor();
            }
            else if (character is Humanoid humanoid && humanoid.IsAttached())
            {
                humanoid.AttachStop();
            }
        }

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            vikingAi.ClearSteering();
            WyrdrasilSeatDebug.Log(character, "Cleared WyrdrasilVikingNpcAI steering state");
        }

        var slotController = character.GetComponent<WyrdrasilAssignedSlotController>();
        if (slotController != null)
        {
            slotController.ReleaseControl();
            WyrdrasilSeatDebug.Log(character, "Released WyrdrasilAssignedSlotController");
        }

        var routeController = character.GetComponent<WyrdrasilRouteTraversalController>();
        if (routeController != null)
        {
            routeController.ReleaseControl();
            WyrdrasilSeatDebug.Log(character, "Released WyrdrasilRouteTraversalController");
        }

        var routeFollower = character.GetComponent<WyrdrasilVikingRouteFollower>();
        if (routeFollower != null)
        {
            routeFollower.ReleaseControl();
            WyrdrasilSeatDebug.Log(character, "Released legacy WyrdrasilVikingRouteFollower");
        }
    }

    private static WyrdrasilAssignedSlotController EnsureAssignedSlotController(Character character)
    {
        var controller = character.GetComponent<WyrdrasilAssignedSlotController>();
        if (!controller)
        {
            controller = character.gameObject.AddComponent<WyrdrasilAssignedSlotController>();
        }

        return controller;
    }

    private static WyrdrasilRouteTraversalController EnsureRouteController(Character character)
    {
        var controller = character.GetComponent<WyrdrasilRouteTraversalController>();
        if (!controller)
        {
            controller = character.gameObject.AddComponent<WyrdrasilRouteTraversalController>();
        }

        return controller;
    }
}
