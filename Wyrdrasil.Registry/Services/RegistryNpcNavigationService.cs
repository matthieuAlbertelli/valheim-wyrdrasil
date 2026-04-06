using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Registry.Components;

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
        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForRoute(routePoints, slotPosition, 1.8f, 0.3f);
        _log.LogInfo($"Assigned resident configured for waypoint route navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToAssignedSlot(Character character, UnityEngine.Vector3 slotPosition)
    {
        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectMovement(slotPosition, 1.8f, 0.3f);
        _log.LogInfo("Assigned resident configured for direct movement fallback.");
    }

    public void NavigateAlongRouteToSeat(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, UnityEngine.Vector3 seatPosition, UnityEngine.Vector3 seatForward)
    {
        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForSeatRoute(routePoints, seatPosition, seatForward, 1.8f, 0.3f);
        _log.LogInfo($"Assigned resident configured for designated seat navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToSeat(Character character, UnityEngine.Vector3 seatPosition, UnityEngine.Vector3 seatForward)
    {
        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectSeatMovement(seatPosition, seatForward, 1.8f, 0.3f);
        _log.LogInfo("Assigned resident configured for direct designated seat fallback.");
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
