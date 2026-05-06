using System.Collections.Generic;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Core.Persistence;

namespace Wyrdrasil.Construction.Persistence;

public sealed class ConstructionPersistenceParticipant : IWorldPersistenceParticipant
{
    private readonly BlueprintCatalogService _blueprintCatalogService;
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionPersistenceParticipant(
        BlueprintCatalogService blueprintCatalogService,
        ConstructionProjectService constructionProjectService,
        ConstructionDebugLogService debugLogService)
    {
        _blueprintCatalogService = blueprintCatalogService;
        _constructionProjectService = constructionProjectService;
        _debugLogService = debugLogService;
    }

    public string ModuleId => "construction";
    public int SchemaVersion => 1;

    public void ResetForWorldChange()
    {
        _blueprintCatalogService.Clear();
        _constructionProjectService.LoadProjects(new List<Models.ConstructionProjectData>(), 1);
        _debugLogService.Info("Persistence", "Reset construction state for world change.");
    }

    public string CapturePayload()
    {
        var saveData = new ConstructionModuleSaveData
        {
            Blueprints = new List<Models.StructureBlueprintData>(_blueprintCatalogService.Blueprints),
            Projects = new List<Models.ConstructionProjectData>(_constructionProjectService.Projects),
            NextProjectId = _constructionProjectService.NextProjectId
        };

        return WorldPersistenceCoordinator.SerializePayload(saveData);
    }

    public void RestorePayload(string payloadXml)
    {
        var saveData = WorldPersistenceCoordinator.DeserializePayload<ConstructionModuleSaveData>(payloadXml);
        if (saveData == null)
        {
            _blueprintCatalogService.Clear();
            _constructionProjectService.LoadProjects(new List<Models.ConstructionProjectData>(), 1);
            _debugLogService.Info("Persistence", "No construction payload found. Starting from empty state.");
            return;
        }

        _blueprintCatalogService.LoadBlueprints(saveData.Blueprints);
        _constructionProjectService.LoadProjects(saveData.Projects, saveData.NextProjectId);
        _debugLogService.Info("Persistence", $"Restored {saveData.Blueprints.Count} blueprints and {saveData.Projects.Count} projects.");
    }

    public bool RetryDeferredResolutions()
    {
        return false;
    }
}
