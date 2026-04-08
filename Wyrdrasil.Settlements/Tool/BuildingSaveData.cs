using System;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class BuildingSaveData
{
    public int Id;
    public string DisplayName = string.Empty;
    public Float3SaveData AnchorPosition = new();
}
