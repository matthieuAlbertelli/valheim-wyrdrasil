using System;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class ZoneSlotSaveData
{
    public int Id;
    public int BuildingId;
    public int ZoneId;
    public ZoneSlotType SlotType;
    public Float3SaveData Position = new();
}
