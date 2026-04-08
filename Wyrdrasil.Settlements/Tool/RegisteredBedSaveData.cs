using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Tool;


[Serializable]
public sealed class RegisteredBedSaveData
{
    public int Id;
    public int BuildingId;
    public int ZoneId;
    public string DisplayName = string.Empty;
    public string PersistentFurnitureId = string.Empty;
    public Float3SaveData SleepPosition = new();
    public Float3SaveData SleepForward = new();
}
