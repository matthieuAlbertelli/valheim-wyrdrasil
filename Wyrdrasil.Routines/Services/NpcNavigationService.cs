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
    private readonly OccupationNavigationStrategyRegistry _strategyRegistry;

    public NpcNavigationService(ManualLogSource log, OccupationNavigationStrategyRegistry strategyRegistry)
    {
        _log = log;
        _strategyRegistry = strategyRegistry;
    }

    public void NavigateAlongRoute(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        if (!_strategyRegistry.TryGetStrategy(target.Execution.NavigationStrategyId, out var strategy))
        {
            _log.LogWarning($"Missing occupation navigation strategy '{target.Execution.NavigationStrategyId}' for {target.Reference}. Falling back to direct position navigation.");
            NavigateAlongRouteToPosition(character, routePoints, target.Plan.EngagePosition, 0.3f, target.Plan.FacingDirection);
            return;
        }

        strategy.NavigateAlongRoute(this, character, routePoints, target);
    }

    public void NavigateDirectly(Character character, OccupationTarget target)
    {
        if (!_strategyRegistry.TryGetStrategy(target.Execution.NavigationStrategyId, out var strategy))
        {
            _log.LogWarning($"Missing occupation navigation strategy '{target.Execution.NavigationStrategyId}' for {target.Reference}. Falling back to direct position navigation.");
            NavigateDirectlyToPosition(character, target.Plan.EngagePosition, 0.3f, target.Plan.FacingDirection);
            return;
        }

        strategy.NavigateDirectly(this, character, target);
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

    public void NavigateAlongRouteToSeat(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateAlongRouteToSeat target={target.Reference} routeCount={routePoints.Count}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToSeat(routePoints, target.Plan.ApproachPosition, target.Plan.EngagePosition, target.Plan.FacingDirection, target.Execution.ChairComponent);
            _log.LogInfo($"Assigned registry viking configured for designated seat navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForSeatRoute(
            routePoints,
            target.Plan.ApproachPosition,
            target.Plan.EngagePosition,
            target.Plan.FacingDirection,
            target.Execution.ChairComponent,
            1.15f,
            0.25f);
        _log.LogInfo($"Assigned resident configured for designated seat navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToSeat(Character character, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateDirectlyToSeat target={target.Reference} approach={target.Plan.ApproachPosition} seat={target.Plan.EngagePosition}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            vikingAi.StartSeatApproach(target.Plan.ApproachPosition, target.Plan.EngagePosition, target.Plan.FacingDirection, target.Execution.ChairComponent, false);
            _log.LogInfo("Assigned registry viking configured for direct designated seat fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectSeatMovement(
            target.Plan.ApproachPosition,
            target.Plan.EngagePosition,
            target.Plan.FacingDirection,
            target.Execution.ChairComponent,
            1.15f,
            0.25f);
        _log.LogInfo("Assigned resident configured for direct designated seat fallback.");
    }

    public void NavigateAlongRouteToBed(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateAlongRouteToBed target={target.Reference} routeCount={routePoints.Count}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToBed(routePoints, target.Plan.ApproachPosition, target.Plan.EngagePosition, target.Plan.FacingDirection, target.Execution.BedComponent, target.Execution.AttachPoint);
            _log.LogInfo($"Assigned registry viking configured for bed navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForBedRoute(
            routePoints,
            target.Plan.ApproachPosition,
            target.Plan.EngagePosition,
            target.Plan.FacingDirection,
            target.Execution.BedComponent,
            target.Execution.AttachPoint,
            1.15f,
            0.25f);
        _log.LogInfo($"Assigned resident configured for bed navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToBed(Character character, OccupationTarget target)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateDirectlyToBed target={target.Reference} approach={target.Plan.ApproachPosition} sleep={target.Plan.EngagePosition}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            vikingAi.StartBedApproach(target.Plan.ApproachPosition, target.Plan.EngagePosition, target.Plan.FacingDirection, target.Execution.BedComponent, target.Execution.AttachPoint, false);
            _log.LogInfo("Assigned registry viking configured for direct bed fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectBedMovement(
            target.Plan.ApproachPosition,
            target.Plan.EngagePosition,
            target.Plan.FacingDirection,
            target.Execution.BedComponent,
            target.Execution.AttachPoint,
            1.15f,
            0.25f);
        _log.LogInfo("Assigned resident configured for direct bed fallback.");
    }

    private static WyrdrasilRouteTraversalController EnsureRouteController(Character character)
    {
        if (!character.TryGetComponent<WyrdrasilRouteTraversalController>(out var controller))
        {
            controller = character.gameObject.AddComponent<WyrdrasilRouteTraversalController>();
        }

        return controller;
    }

    private static WyrdrasilAssignedSlotController EnsureAssignedSlotController(Character character)
    {
        if (!character.TryGetComponent<WyrdrasilAssignedSlotController>(out var controller))
        {
            controller = character.gameObject.AddComponent<WyrdrasilAssignedSlotController>();
        }

        return controller;
    }

    private static void ReleaseLegacyControllers(Character character, bool detachIfAttached)
    {
        if (character.TryGetComponent<WyrdrasilAssignedSlotController>(out var slotController))
        {
            slotController.ReleaseControl();
        }

        if (character.TryGetComponent<WyrdrasilRouteTraversalController>(out var routeController))
        {
            routeController.ReleaseControl();
        }
    }
}
