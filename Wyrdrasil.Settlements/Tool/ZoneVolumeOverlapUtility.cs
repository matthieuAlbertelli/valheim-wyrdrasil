using System.Collections.Generic;
using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public static class ZoneVolumeOverlapUtility
{
    public static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds(root.transform.position, Vector3.zero);

        var hasBounds = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }

    public static bool IntersectsZone(FunctionalZoneData zone, Vector3 point)
    {
        return zone.ContainsPoint(point);
    }

    public static bool IntersectsZone(FunctionalZoneData zone, Bounds bounds)
    {
        if (bounds.max.y < zone.BaseY || bounds.min.y > zone.TopY)
        {
            return false;
        }

        var rectCorners = new[]
        {
            new Vector2(bounds.min.x, bounds.min.z),
            new Vector2(bounds.min.x, bounds.max.z),
            new Vector2(bounds.max.x, bounds.max.z),
            new Vector2(bounds.max.x, bounds.min.z)
        };

        foreach (var corner in rectCorners)
        {
            if (zone.ContainsPointHorizontally(new Vector3(corner.x, zone.Position.y, corner.y)))
            {
                return true;
            }
        }

        foreach (var footprintPoint in zone.FootprintPoints)
        {
            if (footprintPoint.x >= bounds.min.x && footprintPoint.x <= bounds.max.x &&
                footprintPoint.y >= bounds.min.z && footprintPoint.y <= bounds.max.z)
            {
                return true;
            }
        }

        for (var i = 0; i < zone.FootprintPoints.Count; i++)
        {
            var polygonA = zone.FootprintPoints[i];
            var polygonB = zone.FootprintPoints[(i + 1) % zone.FootprintPoints.Count];

            for (var edgeIndex = 0; edgeIndex < 4; edgeIndex++)
            {
                var rectA = rectCorners[edgeIndex];
                var rectB = rectCorners[(edgeIndex + 1) % 4];
                if (SegmentsIntersect(polygonA, polygonB, rectA, rectB))
                {
                    return true;
                }
            }
        }

        var center = bounds.center;
        return zone.ContainsPointHorizontally(center);
    }

    private static bool SegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var o1 = Orientation(a1, a2, b1);
        var o2 = Orientation(a1, a2, b2);
        var o3 = Orientation(b1, b2, a1);
        var o4 = Orientation(b1, b2, a2);

        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        return (o1 == 0 && OnSegment(a1, b1, a2)) ||
               (o2 == 0 && OnSegment(a1, b2, a2)) ||
               (o3 == 0 && OnSegment(b1, a1, b2)) ||
               (o4 == 0 && OnSegment(b1, a2, b2));
    }

    private static int Orientation(Vector2 a, Vector2 b, Vector2 c)
    {
        var value = ((b.y - a.y) * (c.x - b.x)) - ((b.x - a.x) * (c.y - b.y));
        if (Mathf.Abs(value) <= 0.0001f)
        {
            return 0;
        }

        return value > 0f ? 1 : 2;
    }

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 c)
    {
        return b.x <= Mathf.Max(a.x, c.x) + 0.0001f && b.x + 0.0001f >= Mathf.Min(a.x, c.x) &&
               b.y <= Mathf.Max(a.y, c.y) + 0.0001f && b.y + 0.0001f >= Mathf.Min(a.y, c.y);
    }
}
