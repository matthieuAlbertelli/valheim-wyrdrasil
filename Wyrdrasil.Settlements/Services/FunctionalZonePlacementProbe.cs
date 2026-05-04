using UnityEngine;

namespace Wyrdrasil.Settlements.Services;

public sealed class FunctionalZonePlacementProbe
{
    private const float PlacementRayDistance = 100f;
    private const float PlacementYOffset = 0.05f;
    private const float SurfaceUpDotThreshold = 0.55f;

    public bool TryGetPlacementPoint(out Vector3 placementPoint)
    {
        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            placementPoint = Vector3.zero;
            return false;
        }

        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, PlacementRayDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsValidSupportSurface(hitInfo.normal))
                {
                    placementPoint = Vector3.zero;
                    return false;
                }

                placementPoint = hitInfo.point;
                placementPoint.y += PlacementYOffset;
                return true;
            }
        }

        placementPoint = Vector3.zero;
        return false;
    }

    private static bool IsValidSupportSurface(Vector3 normal) => Vector3.Dot(normal.normalized, Vector3.up) >= SurfaceUpDotThreshold;
}
