using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Diagnostics;
using Wyrdrasil.Construction.Models;
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

    public bool TryPurgeAllConstructionInZone(FunctionalZoneData zone, out ZoneConstructionPurgeReport report, out string failureReason)
    {
        report = new ZoneConstructionPurgeReport();

        report.DestroyedPieceCount = DestroyAllPiecesIntersectingZone(zone);

        var projectIds = _constructionProjectService.GetProjectIdsIntersectingZone(zone);
        foreach (var projectId in projectIds)
        {
            if (!_constructionProjectService.DeleteProject(projectId))
            {
                failureReason = $"Failed to delete construction project {projectId}.";
                return false;
            }

            report.DeletedProjectCount++;
        }

        if (report.DestroyedPieceCount <= 0 && report.DeletedProjectCount <= 0)
        {
            failureReason = $"No construction piece or chantier was found inside zone #{zone.Id}.";
            return false;
        }

        failureReason = string.Empty;
        _debugLogService.Info(
            "Cleanup",
            $"Purged all construction in zone #{zone.Id}: destroyed {report.DestroyedPieceCount} piece instance(s) and deleted {report.DeletedProjectCount} project(s).");
        return true;
    }

    private static int DestroyAllPiecesIntersectingZone(FunctionalZoneData zone)
    {
        var destroyedRootIds = new HashSet<int>();
        var destroyedCount = 0;

        foreach (var piece in Object.FindObjectsByType<Piece>(FindObjectsSortMode.None))
        {
            if (piece == null)
            {
                continue;
            }

            var root = piece.gameObject;
            var instanceId = root.GetInstanceID();
            if (destroyedRootIds.Contains(instanceId))
            {
                continue;
            }

            if (!ZoneVolumeOverlapUtility.TryGetWorldBounds(root, out var bounds))
            {
                bounds = new Bounds(root.transform.position, Vector3.zero);
            }

            if (!ZoneVolumeOverlapUtility.IntersectsZone(zone, bounds))
            {
                continue;
            }

            destroyedRootIds.Add(instanceId);
            Object.Destroy(root);
            destroyedCount++;
        }

        return destroyedCount;
    }
}
