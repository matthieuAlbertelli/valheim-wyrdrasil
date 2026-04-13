using System.Collections.Generic;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Persistence;

public sealed class ConstructionModuleSaveData
{
    public List<StructureBlueprintData> Blueprints { get; set; } = new();
    public List<ConstructionProjectData> Projects { get; set; } = new();
    public int NextProjectId { get; set; } = 1;
}
