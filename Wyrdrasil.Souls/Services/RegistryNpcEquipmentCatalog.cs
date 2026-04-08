using System.Collections.Generic;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryNpcEquipmentCatalog
{
    public IReadOnlyList<VikingEquipmentData> VillagerLoadouts { get; } = new[]
    {
        new VikingEquipmentData(null, "ArmorRagsChest", "ArmorRagsLegs", null, null, null),
        new VikingEquipmentData(null, "ArmorLeatherChest", "ArmorLeatherLegs", null, null, null),
        new VikingEquipmentData(null, "ArmorLeatherChest", "ArmorLeatherLegs", "CapeDeerHide", null, null),
        new VikingEquipmentData("HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs", null, "KnifeFlint", null)
    };

    public IReadOnlyList<VikingEquipmentData> InnkeeperLoadouts { get; } = new[]
    {
        new VikingEquipmentData(null, "ArmorRagsChest", "ArmorRagsLegs", null, null, null),
        new VikingEquipmentData(null, "ArmorLeatherChest", "ArmorLeatherLegs", "CapeDeerHide", "KnifeFlint", null),
        new VikingEquipmentData(null, "ArmorLeatherChest", "ArmorLeatherLegs", null, null, null)
    };

    public IReadOnlyList<VikingEquipmentData> GuardLoadouts { get; } = new[]
    {
        new VikingEquipmentData("HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs", "CapeDeerHide", "SwordIron", "ShieldWood"),
        new VikingEquipmentData("HelmetIron", "ArmorIronChest", "ArmorIronLegs", "CapeLinen", "MaceBronze", "ShieldWood")
    };

    public IReadOnlyList<VikingEquipmentData> BlacksmithLoadouts { get; } = new[]
    {
        new VikingEquipmentData("HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs", null, "AxeBronze", null),
        new VikingEquipmentData(null, "ArmorLeatherChest", "ArmorLeatherLegs", null, "Hammer", null)
    };
}
