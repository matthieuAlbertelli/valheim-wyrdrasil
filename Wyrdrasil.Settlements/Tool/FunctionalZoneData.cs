using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;


public sealed class FunctionalZoneData
{
    private readonly List<Vector2> _footprintPoints;

    public int Id { get; }

    public int BuildingId { get; }

    public ZoneType ZoneType { get; }

    public Vector3 Position { get; }

    public IReadOnlyList<Vector2> FootprintPoints => _footprintPoints;

    public float BaseY { get; }

    public float TopY { get; }

    public int LevelIndex { get; }

    public FunctionalZoneData(
        int id,
        int buildingId,
        ZoneType zoneType,
        Vector3 position,
        IEnumerable<Vector2> footprintPoints,
        float baseY,
        float topY,
        int levelIndex)
    {
        if (topY <= baseY)
        {
            throw new ArgumentOutOfRangeException(nameof(topY), "TopY must be greater than BaseY.");
        }

        Id = id;
        BuildingId = buildingId;
        ZoneType = zoneType;
        Position = position;
        BaseY = baseY;
        TopY = topY;
        LevelIndex = levelIndex;

        _footprintPoints = footprintPoints?.ToList() ?? throw new ArgumentNullException(nameof(footprintPoints));
        if (_footprintPoints.Count < 3)
        {
            throw new ArgumentException("A functional zone footprint must contain at least 3 points.", nameof(footprintPoints));
        }
    }

    public bool ContainsPoint(Vector3 point)
    {
        if (point.y < BaseY || point.y > TopY)
        {
            return false;
        }

        return ContainsPointHorizontally(point);
    }

    public bool ContainsPointHorizontally(Vector3 point)
    {
        return IsPointInsidePolygon(new Vector2(point.x, point.z));
    }

    public Vector3 GetDisplayPosition(float yOffset = 0.03f)
    {
        return new Vector3(Position.x, Position.y + yOffset, Position.z);
    }

    private bool IsPointInsidePolygon(Vector2 point)
    {
        var inside = false;
        for (var i = 0; i < _footprintPoints.Count; i++)
        {
            var a = _footprintPoints[i];
            var b = _footprintPoints[(i + 1) % _footprintPoints.Count];

            var intersects = ((a.y > point.y) != (b.y > point.y)) &&
                             (point.x < ((b.x - a.x) * (point.y - a.y) / ((b.y - a.y) + Mathf.Epsilon)) + a.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}