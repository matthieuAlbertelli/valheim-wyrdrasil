using System.Collections.Generic;

namespace Wyrdrasil.Construction.Models;

public sealed class ConstructionValidationReport
{
    public int ProjectId { get; set; }
    public bool IsValid { get; set; }
    public List<string> Messages { get; set; } = new();
}
