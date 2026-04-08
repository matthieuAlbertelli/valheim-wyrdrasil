namespace Wyrdrasil.Registry.Tool;

public sealed class VikingEquipmentData
{
    public string? HelmetItem { get; }
    public string? ChestItem { get; }
    public string? LegItem { get; }
    public string? ShoulderItem { get; }
    public string? RightHandItem { get; }
    public string? LeftHandItem { get; }

    public VikingEquipmentData(
        string? helmetItem,
        string? chestItem,
        string? legItem,
        string? shoulderItem,
        string? rightHandItem,
        string? leftHandItem)
    {
        HelmetItem = helmetItem;
        ChestItem = chestItem;
        LegItem = legItem;
        ShoulderItem = shoulderItem;
        RightHandItem = rightHandItem;
        LeftHandItem = leftHandItem;
    }
}
