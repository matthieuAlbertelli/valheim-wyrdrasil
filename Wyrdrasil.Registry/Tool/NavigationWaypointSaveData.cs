using System;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class NavigationWaypointSaveData
{
    public int Id;
    public Float3SaveData Position = new();
}
