using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services;

public sealed class FunctionalZoneQueryService
{
    public FunctionalZoneData? FindZoneContainingPoint(IReadOnlyList<FunctionalZoneData> zones, Vector3 point) =>
        zones.Where(zone => zone.ContainsPoint(point))
             .OrderBy(zone => HorizontalDistance(zone.Position, point))
             .FirstOrDefault();

    public FunctionalZoneData? FindZoneContainingPoint(IReadOnlyList<FunctionalZoneData> zones, Vector3 point, ZoneType zoneType) =>
        zones.Where(zone => zone.ZoneType == zoneType && zone.ContainsPoint(point))
             .OrderBy(zone => HorizontalDistance(zone.Position, point))
             .FirstOrDefault();

    public FunctionalZoneData? FindZoneContainingPointHorizontally(IReadOnlyList<FunctionalZoneData> zones, Vector3 point) =>
        zones.Where(zone => zone.ContainsPointHorizontally(point))
             .OrderBy(zone => HorizontalDistance(zone.Position, point))
             .FirstOrDefault();

    public FunctionalZoneData? FindZoneContainingPointHorizontally(IReadOnlyList<FunctionalZoneData> zones, Vector3 point, ZoneType zoneType) =>
        zones.Where(zone => zone.ZoneType == zoneType && zone.ContainsPointHorizontally(point))
             .OrderBy(zone => HorizontalDistance(zone.Position, point))
             .FirstOrDefault();

    public bool TryFindZoneAtPoint(IReadOnlyList<FunctionalZoneData> zones, Vector3 point, out FunctionalZoneData zone)
    {
        var match = FindZoneContainingPoint(zones, point);
        if (match != null)
        {
            zone = match;
            return true;
        }

        zone = null!;
        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
}
