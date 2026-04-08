using System.Collections.Generic;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Routines.Components;


/// <summary>
/// Legacy compatibility shim.
/// The route execution has been moved to WyrdrasilRouteTraversalController.
/// This component now simply forwards configuration calls to the new controller
/// so old references can still compile safely.
/// </summary>
public sealed class WyrdrasilVikingRouteFollower : MonoBehaviour
{
    private WyrdrasilRouteTraversalController? _controller;

    private void Awake()
    {
        _controller = GetComponent<WyrdrasilRouteTraversalController>();
        if (_controller == null)
        {
            _controller = gameObject.AddComponent<WyrdrasilRouteTraversalController>();
        }
    }

    public void ConfigureRouteToPosition(
        IReadOnlyList<Vector3> routePoints,
        Vector3 finalDestination,
        float finalStopDistance,
        Vector3 finalFacingDirection)
    {
        EnsureController();
        _controller!.ConfigureRouteToPosition(routePoints, finalDestination, finalStopDistance, finalFacingDirection);
        enabled = false;
    }

    public void ConfigureRouteToSeat(IReadOnlyList<Vector3> routePoints, RegisteredSeatData seat)
    {
        EnsureController();
        _controller!.ConfigureRouteToSeat(routePoints, seat);
        enabled = false;
    }

    public void ReleaseControl()
    {
        EnsureController();
        _controller!.ReleaseControl();
        enabled = false;
    }

    private void EnsureController()
    {
        if (_controller == null)
        {
            _controller = GetComponent<WyrdrasilRouteTraversalController>();
            if (_controller == null)
            {
                _controller = gameObject.AddComponent<WyrdrasilRouteTraversalController>();
            }
        }
    }
}
