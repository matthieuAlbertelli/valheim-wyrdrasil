using System;

namespace Wyrdrasil.Souls.Tool;


[Serializable]
public sealed class VikingIdentitySaveData
{
    public int GenerationSeed;
    public NpcRole Role;
    public VikingAppearanceSaveData Appearance = new();
    public VikingEquipmentSaveData Equipment = new();
}
