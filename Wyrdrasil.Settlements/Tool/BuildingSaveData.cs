using System;
using System.Collections.Generic;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Tool;

[Serializable]
public sealed class BuildingSaveData
{
    public int Id;
    public string DisplayName = string.Empty;
    public Float3SaveData AnchorPosition = new();
    public List<Float2SaveData> FootprintPoints = new();
    public float BaseY;
    public float TopY;
    public int LevelIndex;
}
