using UnityEngine;

namespace Wyrdrasil.Souls.Tool;


public sealed class VikingAppearanceData
{
    public int ModelIndex { get; }
    public string HairItem { get; }
    public string? BeardItem { get; }
    public Color SkinColor { get; }
    public Color HairColor { get; }

    public bool IsFemale => ModelIndex == 1;

    public VikingAppearanceData(int modelIndex, string hairItem, string? beardItem, Color skinColor, Color hairColor)
    {
        ModelIndex = modelIndex;
        HairItem = hairItem;
        BeardItem = beardItem;
        SkinColor = skinColor;
        HairColor = hairColor;
    }
}
