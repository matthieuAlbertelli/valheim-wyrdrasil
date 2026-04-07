using System;
using System.Collections.Generic;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class FunctionalZoneSaveData
{
    public int Id;
    public int BuildingId;
    public ZoneType ZoneType;
    public Float3SaveData Position = new();
    public List<Float2SaveData> FootprintPoints = new();
    public float BaseY;
    public float TopY;
    public int LevelIndex;
}
