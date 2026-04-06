using BepInEx.Logging;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentRuntimeService
{
    private readonly ManualLogSource _log;
    private readonly RegistryNpcNavigationService _navigationService;
    private readonly RegistryWaypointService _waypointService;

    public RegistryResidentRuntimeService(
        ManualLogSource log,
        RegistryNpcNavigationService navigationService,
        RegistryWaypointService waypointService)
    {
        _log = log;
        _navigationService = navigationService;
        _waypointService = waypointService;
    }

    public void ApplyInnkeeperAssignment(RegisteredNpcData resident, ZoneSlotData slot)
    {
        ApplyPositionAssignment(resident, slot.Position, $"innkeeper slot #{slot.Id}");
    }

    public void ApplySeatAssignment(RegisteredNpcData resident, RegisteredSeatData seat)
    {
        var character = resident.Character;

        if (!character)
        {
            _log.LogWarning($"Cannot apply assignment to seat #{seat.Id}: resident character is no longer available.");
            return;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, seat.SeatPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToSeat(character, routePoints, seat.SeatPosition, seat.SeatForward);
            _log.LogInfo($"Applied navigation graph route with {routePoints.Count} waypoint step(s) for seat #{seat.Id}.");
            return;
        }

        _navigationService.NavigateDirectlyToSeat(character, seat.SeatPosition, seat.SeatForward);
        _log.LogWarning($"No connected waypoint route was found for seat #{seat.Id}. Falling back to direct movement.");
    }

    private void ApplyPositionAssignment(RegisteredNpcData resident, UnityEngine.Vector3 targetPosition, string targetLabel)
    {
        var character = resident.Character;

        if (!character)
        {
            _log.LogWarning($"Cannot apply assignment to {targetLabel}: resident character is no longer available.");
            return;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, targetPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRoute(character, routePoints, targetPosition);
            _log.LogInfo($"Applied navigation graph route with {routePoints.Count} waypoint step(s) for {targetLabel}.");
            return;
        }

        _navigationService.NavigateDirectlyToAssignedSlot(character, targetPosition);
        _log.LogWarning($"No connected waypoint route was found for {targetLabel}. Falling back to direct movement.");
    }
}
