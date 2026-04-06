using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

public sealed class FunctionalZoneData
{
    public int Id { get; }

    public ZoneType ZoneType { get; }

    public Vector3 Position { get; }

    public float Radius { get; }

    public FunctionalZoneData(int id, ZoneType zoneType, Vector3 position, float radius)
    {
        Id = id;
        ZoneType = zoneType;
        Position = position;
        Radius = radius;
    }
}
