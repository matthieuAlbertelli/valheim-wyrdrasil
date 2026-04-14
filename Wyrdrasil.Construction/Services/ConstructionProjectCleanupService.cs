using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectCleanupService
{
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionDebugLogService _debugLogService;

    public ConstructionProjectCleanupService(
        ConstructionProjectService constructionProjectService,
        ConstructionDebugLogService debugLogService)
    {
        _constructionProjectService = constructionProjectService;
        _debugLogService = debugLogService;
    }

    public bool TryDeleteProjectsInZone(FunctionalZoneData zone, out int deletedProjectCount, out int destroyedPieceCount, out string failureReason)
    {
        deletedProjectCount = 0;
        destroyedPieceCount = 0;

        var projectIds = _constructionProjectService.GetProjectIdsInZone(zone);
        if (projectIds.Count == 0)
        {
            failureReason = $"No construction project was found inside zone #{zone.Id}.";
            return false;
        }

        foreach (var projectId in projectIds)
        {
            destroyedPieceCount += DestroyBuiltPiecesForProject(projectId);
            if (!_constructionProjectService.DeleteProject(projectId))
            {
                failureReason = $"Failed to delete construction project {projectId}.";
                return false;
            }

            deletedProjectCount++;
        }

        failureReason = string.Empty;
        _debugLogService.Info(
            "Cleanup",
            $"Deleted {deletedProjectCount} construction project(s) in zone #{zone.Id} and destroyed {destroyedPieceCount} built piece instance(s).");
        return true;
    }

    private static int DestroyBuiltPiecesForProject(int projectId)
    {
        var marker = $"_ConstructionProject_{projectId}_Piece_";
        var destroyedRootIds = new HashSet<int>();
        var destroyedCount = 0;

        foreach (var piece in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None))
        {
            if (piece == null)
            {
                continue;
            }

            var root = piece.gameObject;
            if (!root.name.Contains(marker))
            {
                continue;
            }

            var instanceId = root.GetInstanceID();
            if (!destroyedRootIds.Add(instanceId))
            {
                continue;
            }

            Object.Destroy(root);
            destroyedCount++;
        }

        return destroyedCount;
    }
}
