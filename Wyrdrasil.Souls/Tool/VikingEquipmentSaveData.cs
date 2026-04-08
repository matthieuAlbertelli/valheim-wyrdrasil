using System;

namespace Wyrdrasil.Souls.Tool;


[Serializable]
public sealed class VikingEquipmentSaveData
{
    public string HelmetItem = string.Empty;
    public bool HasHelmetItem;
    public string ChestItem = string.Empty;
    public bool HasChestItem;
    public string LegItem = string.Empty;
    public bool HasLegItem;
    public string ShoulderItem = string.Empty;
    public bool HasShoulderItem;
    public string RightHandItem = string.Empty;
    public bool HasRightHandItem;
    public string LeftHandItem = string.Empty;
    public bool HasLeftHandItem;
}
