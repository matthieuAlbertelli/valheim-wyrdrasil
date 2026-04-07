using System.Collections.Generic;
using BepInEx.Logging;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Diagnostics;
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
            ReleaseLegacyControllers(character, true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToPosition(routePoints, slotPosition, 0.3f, slotPosition - character.transform.position);
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
            ReleaseLegacyControllers(character, true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToPosition(System.Array.Empty<UnityEngine.Vector3>(), slotPosition, 0.3f, slotPosition - character.transform.position);
            _log.LogInfo("Assigned registry viking configured for deterministic direct movement fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectMovement(slotPosition, 1.8f, 0.3f);
        _log.LogInfo("Assigned resident configured for direct movement fallback.");
    }

    public void NavigateAlongRouteToSeat(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, RegisteredSeatData seat)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateAlongRouteToSeat seatId={seat.Id} routeCount={routePoints.Count}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToSeat(routePoints, seat);
            _log.LogInfo($"Assigned registry viking configured for designated seat navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForSeatRoute(routePoints, seat.ApproachPosition, seat.SeatPosition, seat.SeatForward, seat.ChairComponent, 1.8f, 0.25f);
        _log.LogInfo($"Assigned resident configured for designated seat navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToSeat(Character character, RegisteredSeatData seat)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateDirectlyToSeat seatId={seat.Id} approach={seat.ApproachPosition} seat={seat.SeatPosition}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.StartSeatApproach(seat, false);
            _log.LogInfo("Assigned registry viking configured for direct designated seat fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectSeatMovement(seat.ApproachPosition, seat.SeatPosition, seat.SeatForward, seat.ChairComponent, 1.8f, 0.25f);
        _log.LogInfo("Assigned resident configured for direct designated seat fallback.");
    }

    public void NavigateAlongRouteToBed(Character character, IReadOnlyList<UnityEngine.Vector3> routePoints, RegisteredBedData bed)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateAlongRouteToBed bedId={bed.Id} routeCount={routePoints.Count}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            var routeController = EnsureRouteController(character);
            routeController.ConfigureRouteToBed(routePoints, bed);
            _log.LogInfo($"Assigned registry viking configured for bed navigation with {routePoints.Count} waypoint(s).");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForBedRoute(routePoints, bed.ApproachPosition, bed.SleepPosition, bed.SleepForward, bed.BedComponent, bed.SleepAttachPoint, 1.8f, 0.25f);
        _log.LogInfo($"Assigned resident configured for bed navigation with {routePoints.Count} waypoint(s).");
    }

    public void NavigateDirectlyToBed(Character character, RegisteredBedData bed)
    {
        WyrdrasilSeatDebug.Log(character, $"NavigateDirectlyToBed bedId={bed.Id} approach={bed.ApproachPosition} sleep={bed.SleepPosition}");

        var vikingAi = character.GetComponent<WyrdrasilVikingNpcAI>();
        if (vikingAi != null)
        {
            ReleaseLegacyControllers(character, true);
            vikingAi.StartBedApproach(bed, false);
            _log.LogInfo("Assigned registry viking configured for direct bed fallback.");
            return;
        }

        var controller = EnsureAssignedSlotController(character);
        controller.ConfigureForDirectBedMovement(bed.ApproachPosition, bed.SleepPosition, bed.SleepForward, bed.BedComponent, bed.SleepAttachPoint, 1.8f, 0.25f);
        _log.LogInfo("Assigned resident configured for direct bed fallback.");
    }

    public void ReleaseOccupation(Character character, bool detachIfAttached = true)
    {
        ReleaseLegacyControllers(character, detachIfAttached);
    }

    private static void ReleaseLegacyControllers(Character character, bool detachIfAttached)
    {
        if (detachIfAttached && character is Humanoid humanoid && humanoid.IsAttached())
        {
            humanoid.AttachStop();
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
