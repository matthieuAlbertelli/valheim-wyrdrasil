using UnityEngine;

namespace Wyrdrasil.Registry.Tool;

public sealed class NavigationWaypointData
{
    public int Id { get; }

    public Vector3 Position { get; }

    public NavigationWaypointData(int id, Vector3 position)
    {
        Id = id;
        Position = position;
    }
}
