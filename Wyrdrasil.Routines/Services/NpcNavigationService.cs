using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Core.Tool;
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
        if (slotController != null && slotController.IsControlActive)
        {
            return true;
        }

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        return vikingAi != null && vikingAi.IsRegistryNavigationActive;
    }

    public void ReleaseOccupation(Character character, bool detachIfAttached = true)
    {
        ReleaseLegacyControllers(character, detachIfAttached);
    }

    public void NavigateAlongRouteToAnchor(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        var anchorPlan = OccupationAnchorNavigationPlan.FromTarget(target);
        WyrdrasilSeatDebug.Log(character, $"NavigateAlongRouteToAnchor target={target.Reference} anchor={anchorPlan.AnchorKind} routeCount={routePoints.Count}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToAnchor(routePoints, anchorPlan);
            _log.LogInfo($"Assigned registry viking configured for anchored {anchorPlan.AnchorKind} navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        ConfigureLegacyAnchorMovement(character, routePoints, anchorPlan, useRoute: true);
    }

    public void NavigateDirectlyToAnchor(Character character, OccupationTarget target)
    {
        var anchorPlan = OccupationAnchorNavigationPlan.FromTarget(target);
        WyrdrasilSeatDebug.Log(character, $"NavigateDirectlyToAnchor target={target.Reference} anchor={anchorPlan.AnchorKind} approach={anchorPlan.ApproachPosition} engage={anchorPlan.EngagePosition}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.SetCivilianWalkLocomotion(true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToAnchor(System.Array.Empty<UnityEngine.Vector3>(), anchorPlan);
            _log.LogInfo($"Assigned registry viking configured for direct anchored {anchorPlan.AnchorKind} approach.");
            return;
        }

        ConfigureLegacyAnchorMovement(character, System.Array.Empty<UnityEngine.Vector3>(), anchorPlan, useRoute: false);
    }

    public void NavigateAlongRouteToSeat(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        NavigateAlongRouteToAnchor(character, routePoints, target);
    }

    public void NavigateDirectlyToSeat(Character character, OccupationTarget target)
    {
        NavigateDirectlyToAnchor(character, target);
    }

    public void NavigateAlongRouteToBed(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, OccupationTarget target)
    {
        NavigateAlongRouteToAnchor(character, routePoints, target);
    }

    public void NavigateDirectlyToBed(Character character, OccupationTarget target)
    {
        NavigateDirectlyToAnchor(character, target);
    }

    private void ConfigureLegacyAnchorMovement(
        Character character,
        IReadOnlyList<UnityEngine.Vector3> routePoints,
        OccupationAnchorNavigationPlan anchorPlan,
        bool useRoute)
    {
        var controller = EnsureAssignedSlotController(character);

        switch (anchorPlan.AnchorKind)
        {
            case OccupationAnchorAttachmentKind.Seat:
                if (useRoute)
                {
                    controller.ConfigureForSeatRoute(
                        routePoints,
                        anchorPlan.ApproachPosition,
                        anchorPlan.EngagePosition,
                        anchorPlan.FacingDirection,
                        anchorPlan.ChairComponent,
                        1.15f,
                        0.25f);
                    _log.LogInfo($"Assigned resident configured for anchored seat navigation with {routePoints.Count} waypoint(s).");
                    return;
                }

                controller.ConfigureForDirectSeatMovement(
                    anchorPlan.ApproachPosition,
                    anchorPlan.EngagePosition,
                    anchorPlan.FacingDirection,
                    anchorPlan.ChairComponent,
                    1.15f,
                    0.25f);
                _log.LogInfo("Assigned resident configured for direct anchored seat fallback.");
                return;

            case OccupationAnchorAttachmentKind.Bed:
                if (useRoute)
                {
                    controller.ConfigureForBedRoute(
                        routePoints,
                        anchorPlan.ApproachPosition,
                        anchorPlan.EngagePosition,
                        anchorPlan.FacingDirection,
                        anchorPlan.BedComponent,
                        anchorPlan.AttachPoint,
                        1.15f,
                        0.25f);
                    _log.LogInfo($"Assigned resident configured for anchored bed navigation with {routePoints.Count} waypoint(s).");
                    return;
                }

                controller.ConfigureForDirectBedMovement(
                    anchorPlan.ApproachPosition,
                    anchorPlan.EngagePosition,
                    anchorPlan.FacingDirection,
                    anchorPlan.BedComponent,
                    anchorPlan.AttachPoint,
                    1.15f,
                    0.25f);
                _log.LogInfo("Assigned resident configured for direct anchored bed fallback.");
                return;

            default:
                if (useRoute)
                {
                    controller.ConfigureForRoute(routePoints, anchorPlan.ApproachPosition, 1.15f, anchorPlan.ApproachProfile.ApproachRadius);
                    _log.LogInfo($"Assigned resident configured for generic anchored route fallback with {routePoints.Count} waypoint(s).");
                    return;
                }

                controller.ConfigureForDirectMovement(anchorPlan.ApproachPosition, 1.15f, anchorPlan.ApproachProfile.ApproachRadius);
                _log.LogInfo("Assigned resident configured for generic direct anchored fallback.");
                return;
        }
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

        if (detachIfAttached)
        {
            DetachCharacterFromOccupationAnchor(character);
        }
    }

    private static void DetachCharacterFromOccupationAnchor(Character character)
    {
        if (character is WyrdrasilVikingNpc viking && viking.IsAttached())
        {
            viking.ForceDetachFromCurrentAnchor();
            return;
        }

        if (character is Humanoid humanoid && humanoid.IsAttached())
        {
            humanoid.AttachStop();
        }
    }
}
