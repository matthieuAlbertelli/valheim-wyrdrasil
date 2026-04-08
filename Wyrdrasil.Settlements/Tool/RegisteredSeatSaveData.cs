using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Tool;


[Serializable]
public sealed class RegisteredSeatSaveData
{
    public int Id;
    public int BuildingId;
    public int? ZoneId;
    public SeatUsageType UsageType;
    public string DisplayName = string.Empty;
    public string PersistentFurnitureId = string.Empty;
    public Float3SaveData SeatPosition = new();
}
