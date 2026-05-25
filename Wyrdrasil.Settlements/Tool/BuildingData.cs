using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wyrdrasil.Settlements.Tool;

public sealed class BuildingData
{
    private readonly List<Vector2> _footprintPoints;

    public int Id { get; }

    public string DisplayName { get; }

    public Vector3 AnchorPosition { get; }

    public IReadOnlyList<Vector2> FootprintPoints => _footprintPoints;

    public float BaseY { get; }

    public float TopY { get; }

    public int LevelIndex { get; }

    public bool HasVolume => _footprintPoints.Count >= 3 && TopY > BaseY;

    public BuildingData(int id, string displayName, Vector3 anchorPosition)
        : this(id, displayName, anchorPosition, Enumerable.Empty<Vector2>(), anchorPosition.y, anchorPosition.y, 0)
    {
    }

    public BuildingData(
        int id,
        string displayName,
        Vector3 anchorPosition,
        IEnumerable<Vector2> footprintPoints,
        float baseY,
        float topY,
        int levelIndex)
    {
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"Bâtiment #{id}" : displayName;
        AnchorPosition = anchorPosition;
        BaseY = baseY;
        TopY = topY;
        LevelIndex = levelIndex;

        _footprintPoints = footprintPoints?.ToList() ?? new List<Vector2>();
        if (_footprintPoints.Count > 0 && _footprintPoints.Count < 3)
        {
            throw new ArgumentException("A building footprint must contain at least 3 points when a footprint is provided.", nameof(footprintPoints));
        }

        if (_footprintPoints.Count >= 3 && topY <= baseY)
        {
            throw new ArgumentOutOfRangeException(nameof(topY), "TopY must be greater than BaseY for a volumetric building.");
        }
    }

    public bool ContainsPoint(Vector3 point)
    {
        if (!HasVolume)
        {
            return false;
        }

        if (point.y < BaseY || point.y > TopY)
        {
            return false;
        }

        return ContainsPointHorizontally(point);
    }

    public bool ContainsPointHorizontally(Vector3 point)
    {
        if (_footprintPoints.Count < 3)
        {
            return false;
        }

        return IsPointInsidePolygon(new Vector2(point.x, point.z));
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
