using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Settlements.Tool;


[Serializable]
public sealed class BuildingSaveData
{
    public int Id;
    public string DisplayName = string.Empty;
    public Float3SaveData AnchorPosition = new();
}
