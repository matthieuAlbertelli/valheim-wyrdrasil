namespace Wyrdrasil.Registry.Tool;

public sealed class VikingIdentityData
{
    public int GenerationSeed { get; }
    public NpcRole Role { get; }
    public VikingAppearanceData Appearance { get; }
    public VikingEquipmentData Equipment { get; }

    public VikingIdentityData(int generationSeed, NpcRole role, VikingAppearanceData appearance, VikingEquipmentData equipment)
    {
        GenerationSeed = generationSeed;
        Role = role;
        Appearance = appearance;
        Equipment = equipment;
    }
}
