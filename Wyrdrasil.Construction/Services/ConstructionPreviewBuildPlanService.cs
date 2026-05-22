using System.Collections.Generic;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

/// <summary>
/// Creates non-mutating buildability simulations for blueprint placement preview.
///
/// The synchronous EvaluatePreview method is kept as a compatibility facade. Interactive preview now
/// prefers CreateJob through ConstructionPreviewStabilitySchedulerService so analysis can be debounced,
/// cached and spread over several frames.
/// </summary>
public sealed class ConstructionPreviewBuildPlanService
{
    private readonly ConstructionPieceStabilityProbeService _stabilityProbeService;

    public ConstructionPreviewBuildPlanService(ConstructionPieceStabilityProbeService stabilityProbeService)
    {
        _stabilityProbeService = stabilityProbeService;
    }

    public ConstructionPreviewBuildPlanJob CreateJob(
        StructureBlueprintData blueprint,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        return new ConstructionPreviewBuildPlanJob(_stabilityProbeService, blueprint, placements);
    }

    public IReadOnlyDictionary<int, ConstructionPreviewPieceEvaluation> EvaluatePreview(
        StructureBlueprintData blueprint,
        IReadOnlyList<ConstructionResolvedPiecePlacement> placements)
    {
        var job = CreateJob(blueprint, placements);
        while (!job.IsComplete)
        {
            job.Tick(int.MaxValue);
        }

        return job.CurrentEvaluations;
    }
}
