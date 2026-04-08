using System;
using System.Collections.Generic;

namespace Wyrdrasil.Core.Persistence;

[Serializable]
public sealed class WorldSaveEnvelope
{
    public int Version = 1;
    public List<ModuleSaveSectionData> Sections = new();
}
