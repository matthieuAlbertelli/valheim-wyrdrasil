using System;

namespace Wyrdrasil.Core.Persistence;

[Serializable]
public sealed class ModuleSaveSectionData
{
    public string ModuleId = string.Empty;
    public int SchemaVersion = 1;
    public string PayloadXml = string.Empty;
}
