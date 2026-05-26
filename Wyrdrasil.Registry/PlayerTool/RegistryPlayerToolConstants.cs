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
    public const string InspectActionPiecePrefabNamePrefix = "Wyrdrasil_Action_Inspect_Category_";
    public const string InspectActionDisplayName = "Inspecter";
    public const string InspectActionDescription = "Révèle les informations Wyrdrasil de la cible visée.";

    public const string CreateTavernZoneActionPiecePrefabName = "Wyrdrasil_Action_CreateTavernZone";
    public const string CreateTavernZoneActionDisplayName = "Délimiter taverne";
    public const string CreateTavernZoneActionDescription = "Trace physiquement les limites d'une taverne.";

    public const string MarkActionPiecePrefabName = "Wyrdrasil_Action_Mark";
    public const string MarkActionDisplayName = "Marquer";
    public const string MarkActionDescription = "Enregistre automatiquement le lit, l'établi, la forge ou tout autre objet marquable visé.";

    public const string DesignateBedActionPiecePrefabName = MarkActionPiecePrefabName;
    public const string DesignateBedActionDisplayName = MarkActionDisplayName;
    public const string DesignateBedActionDescription = MarkActionDescription;

    public const string AssignActionPiecePrefabName = "Wyrdrasil_Action_Assign";
    public const string AssignActionDisplayName = "Assigner";
    public const string AssignActionDescription = "Visez un viking enregistré ou un chantier, puis un lit, un établi, une forge, un chantier ou tout autre objet assignable.";

    public const string AssignBedActionPiecePrefabName = AssignActionPiecePrefabName;
    public const string AssignBedActionDisplayName = AssignActionDisplayName;
    public const string AssignBedActionDescription = AssignActionDescription;

    public const string SpawnAndRegisterVikingActionPiecePrefabName = "Wyrdrasil_Action_SpawnAndRegisterViking";
    public const string SpawnAndRegisterVikingActionDisplayName = "Appeler viking";
    public const string SpawnAndRegisterVikingActionDescription = "Fait apparaître un viking devant vous et l'enregistre comme âme du village.";

    public const string DefineBuildingActionPiecePrefabName = "Wyrdrasil_Action_DefineBuilding";
    public const string DefineBuildingActionDisplayName = "Délimiter bâtiment";
    public const string DefineBuildingActionDescription = "Trace le volume physique d'un bâtiment à capturer en plan.";

    public const string CaptureBuildingBlueprintActionPiecePrefabName = "Wyrdrasil_Action_CaptureBuildingBlueprint";
    public const string CaptureBuildingBlueprintActionDisplayName = "Enregistrer bâtiment";
    public const string CaptureBuildingBlueprintActionDescription = "Capture le bâtiment visé comme modèle de construction.";

    public const int ActionsCategoryIndex = 0;
    public const int PlansCategoryIndex = 1;
    public const string BlueprintPlanActionPiecePrefabNamePrefix = "Wyrdrasil_Blueprint_";

    public const string BlueprintThumbnailHiddenRootName = "Wyrdrasil.Registry.PlayerTool.ThumbnailRenderer";
    public const int BlueprintThumbnailResolution = 256;
    public const float BlueprintThumbnailFieldOfView = 30f;
    public const float BlueprintThumbnailPixelsPerUnit = 100f;

    public static readonly string[] PlayerToolCategoryLabels =
    {
        "Actions",
        "Plans"
    };
}
