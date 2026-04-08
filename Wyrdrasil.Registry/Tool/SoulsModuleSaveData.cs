using System;
using System.Collections.Generic;

namespace Wyrdrasil.Registry.Tool;

[Serializable]
public sealed class SoulsModuleSaveData
{
    public int SchemaVersion = 1;
    public int NextResidentId = 1;
    public List<RegisteredNpcSaveData> Residents = new();
}
