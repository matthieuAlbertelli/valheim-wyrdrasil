using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Tool;


[Serializable]
public sealed class ZoneSlotSaveData
{
    public int Id;
    public int BuildingId;
    public int ZoneId;
    public ZoneSlotType SlotType;
    public Float3SaveData Position = new();
    public Float3SaveData FacingDirection = new();
}
