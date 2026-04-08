using System;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class VikingIdentitySaveData
{
    public int GenerationSeed;
    public NpcRole Role;
    public VikingAppearanceSaveData Appearance = new();
    public VikingEquipmentSaveData Equipment = new();
}
