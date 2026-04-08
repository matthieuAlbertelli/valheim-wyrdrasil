using System;
using Wyrdrasil.Core.Tool;

namespace Wyrdrasil.Souls.Tool;


[Serializable]
public sealed class VikingAppearanceSaveData
{
    public int ModelIndex;
    public string HairItem = string.Empty;
    public string BeardItem = string.Empty;
    public bool HasBeardItem;
    public ColorSaveData SkinColor = new();
    public ColorSaveData HairColor = new();
}
