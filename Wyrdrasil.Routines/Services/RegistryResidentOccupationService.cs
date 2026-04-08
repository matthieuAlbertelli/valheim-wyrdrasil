using BepInEx.Logging;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Services;


public sealed class RegistryResidentOccupationService
{
    private readonly ManualLogSource _log;
    private readonly RegistryResidentRuntimeService _runtimeService;
    private readonly RegistrySlotService _slotService;
    private readonly RegistrySeatService _seatService;
    private readonly RegistryBedService _bedService;
    private readonly RegistryNpcNavigationService _navigationService;
    private readonly RegistryWaypointService _waypointService;

    public RegistryResidentOccupationService(
        ManualLogSource log,
        RegistryResidentRuntimeService runtimeService,
        RegistrySlotService slotService,
        RegistrySeatService seatService,
        RegistryBedService bedService,
        RegistryNpcNavigationService navigationService,
        RegistryWaypointService waypointService)
    {
        _log = log;
        _runtimeService = runtimeService;
        _slotService = slotService;
        _seatService = seatService;
        _bedService = bedService;
        _navigationService = navigationService;
        _waypointService = waypointService;
    }

    public bool TryOccupyAssignedSlot(RegisteredNpcData resident)
    {
        if (!resident.AssignedSlotId.HasValue)
        {
            return false;
        }

        if (!_slotService.TryGetSlotById(resident.AssignedSlotId.Value, out var slotData))
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, slotData.Position, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRoute(character, routePoints, slotData.Position);
            return true;
        }

        _navigationService.NavigateDirectlyToAssignedSlot(character, slotData.Position);
        return true;
    }

    public bool TryOccupyAssignedSeat(RegisteredNpcData resident)
    {
        if (!resident.AssignedSeatId.HasValue)
        {
            return false;
        }

        if (!_seatService.TryGetSeatById(resident.AssignedSeatId.Value, out var seatData))
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, seatData.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToSeat(character, routePoints, seatData);
            return true;
        }

        _navigationService.NavigateDirectlyToSeat(character, seatData);
        return true;
    }

    public bool TryOccupyAssignedBed(RegisteredNpcData resident)
    {
        if (!resident.AssignedBedId.HasValue)
        {
            return false;
        }

        if (!_bedService.TryGetBedById(resident.AssignedBedId.Value, out var bedData))
        {
            return false;
        }

        if (!_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            return false;
        }

        if (_waypointService.TryBuildRoute(character.transform.position, bedData.ApproachPosition, out var routePoints) && routePoints.Count > 0)
        {
            _navigationService.NavigateAlongRouteToBed(character, routePoints, bedData);
            return true;
        }

        _navigationService.NavigateDirectlyToBed(character, bedData);
        return true;
    }

    public void ReleaseOccupation(RegisteredNpcData resident, bool detachIfAttached = true)
    {
        if (_runtimeService.TryGetBoundCharacter(resident.Id, out var character))
        {
            _navigationService.ReleaseOccupation(character, detachIfAttached);
        }
    }
}
