using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Tool;


[Serializable]
public sealed class NavigationWaypointSaveData
{
    public int Id;
    public Float3SaveData Position = new();
}
