using System;

namespace Wyrdrasil.Registry.Tool;

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
