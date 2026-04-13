namespace Wyrdrasil.Construction.Diagnostics;

public sealed class ConstructionDebugOptions
{
    public bool VerboseLoggingEnabled { get; set; }
    public bool IgnoreMaterialRequirements { get; set; }
    public bool InstantCompleteWorkContributions { get; set; }
    public bool AllowInstantBlueprintPlacement { get; set; } = true;
    public bool LogBlueprintCapture { get; set; } = true;
    public bool LogProjectLifecycle { get; set; } = true;
    public bool LogWorkClaims { get; set; } = true;
    public bool LogWorkProgress { get; set; } = true;
    public bool LogPlacementValidation { get; set; } = true;
    public bool LogInstantPlacement { get; set; } = true;
    public bool LogMaterialFlow { get; set; } = true;
}
