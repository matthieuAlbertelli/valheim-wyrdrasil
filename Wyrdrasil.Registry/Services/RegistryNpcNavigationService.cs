using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryNpcNavigationService
{
    private readonly ManualLogSource _log;

    public RegistryNpcNavigationService(ManualLogSource log)
    {
        _log = log;
    }

    public void NavigateAlongRoute(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, UnityEngine.Vector3 slotPosition)
    {
        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyController(character);
            vikingAi.NavigateAlongRoute(routePoints, slotPosition, 0.3f, slotPosition - character.transform.position);
            _log.LogInfo($"Assigned registry viking configured for waypoint route navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForRoute(routePoints, slotPosition, 1.8f, 0.3f);
        _log.LogInfo($"Assigned resident configured for waypoint route navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToAssignedSlot(Character character, UnityEngine.Vector3 slotPosition)
    {
        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyController(character);
            vikingAi.NavigateDirectly(slotPosition, 0.3f, slotPosition - character.transform.position);
            _log.LogInfo("Assigned registry viking configured for direct movement fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectMovement(slotPosition, 1.8f, 0.3f);
        _log.LogInfo("Assigned resident configured for direct movement fallback.");
    }

    public void NavigateAlongRouteToSeat(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, RegisteredSeatData seat)
    {
        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyController(character);
            vikingAi.NavigateAlongRouteToSeat(routePoints, seat, 0.25f);
            _log.LogInfo($"Assigned registry viking configured for designated seat navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForSeatRoute(
            routePoints,
            seat.ApproachPosition,
            seat.SeatPosition,
            seat.SeatForward,
            seat.ChairComponent,
            1.8f,
            0.25f);

        _log.LogInfo($"Assigned resident configured for designated seat navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToSeat(Character character, RegisteredSeatData seat)
    {
        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyController(character);
            vikingAi.NavigateDirectlyToSeat(seat, 0.25f);
            _log.LogInfo("Assigned registry viking configured for direct designated seat fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectSeatMovement(
            seat.ApproachPosition,
            seat.SeatPosition,
            seat.SeatForward,
            seat.ChairComponent,
            1.8f,
            0.25f);

        _log.LogInfo("Assigned resident configured for direct designated seat fallback.");
    }

    private static void ReleaseLegacyController(Character character)
    {
        var controller = character.GetComponent<WyrdrasilAssignedSlotController>();
        if (controller != null)
        {
            controller.ReleaseControl();
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
}