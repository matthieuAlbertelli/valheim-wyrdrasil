namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolConstants
{
    public const string ItemPrefabName = "WyrdrasilRegistry";
    public const string PieceTableName = "WyrdrasilRegistryPieceTable";
    public const string HiddenRootName = "Wyrdrasil.Registry.PlayerTool.HiddenPrefabs";

    public const string SourceItemPrefabName = "Hammer";

    public const string DisplayName = "Registre des Âmes";
    public const string Description = "Révèle les liens invisibles du village.";

    public const string InspectActionPiecePrefabName = "Wyrdrasil_Action_Inspect";
    public const string InspectActionDisplayName = "Inspecter";
    public const string InspectActionDescription = "Révèle les informations Wyrdrasil de la cible visée.";

    public const string CreateTavernZoneActionPiecePrefabName = "Wyrdrasil_Action_CreateTavernZone";
    public const string CreateTavernZoneActionDisplayName = "Délimiter taverne";
    public const string CreateTavernZoneActionDescription = "Trace physiquement les limites d'une taverne.";

    public const string DesignateBedActionPiecePrefabName = "Wyrdrasil_Action_DesignateBed";
    public const string DesignateBedActionDisplayName = "Marquer lit";
    public const string DesignateBedActionDescription = "Enregistre le lit visé comme lit assignable.";

    public const string AssignBedActionPiecePrefabName = "Wyrdrasil_Action_AssignBed";
    public const string AssignBedActionDisplayName = "Assigner lit";
    public const string AssignBedActionDescription = "Visez un viking enregistré, puis un lit marqué.";

    public const string SpawnAndRegisterVikingActionPiecePrefabName = "Wyrdrasil_Action_SpawnAndRegisterViking";
    public const string SpawnAndRegisterVikingActionDisplayName = "Appeler viking";
    public const string SpawnAndRegisterVikingActionDescription = "Fait apparaître un viking devant vous et l'enregistre comme âme du village.";

    public const string CaptureTavernBlueprintActionPiecePrefabName = "Wyrdrasil_Action_CaptureTavernBlueprint";
    public const string CaptureTavernBlueprintActionDisplayName = "Enregistrer taverne";
    public const string CaptureTavernBlueprintActionDescription = "Capture la taverne visée comme modèle de construction.";

    public const int PlansCategoryIndex = 4;
    public const string BlueprintPlanActionPiecePrefabNamePrefix = "Wyrdrasil_Blueprint_";

    public const string BlueprintThumbnailHiddenRootName = "Wyrdrasil.Registry.PlayerTool.ThumbnailRenderer";
    public const int BlueprintThumbnailResolution = 256;
    public const float BlueprintThumbnailFieldOfView = 30f;
    public const float BlueprintThumbnailPixelsPerUnit = 100f;

    public static readonly string[] PlayerToolCategoryLabels =
    {
        "Registre",
        "Lieux",
        "Postes",
        "Âmes",
        "Plans"
    };
}
