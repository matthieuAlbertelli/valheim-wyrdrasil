using UnityEngine;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Occupations;

public sealed class ConstructionWorkPostOccupationTargetSource : IOccupationTargetSource
{
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly AnchorOccupationPlanBuilder _planBuilder;

    public ConstructionWorkPostOccupationTargetSource(
        IConstructionRuntimeApi constructionRuntimeApi,
        ISettlementsRuntimeApi settlementsRuntimeApi,
        AnchorOccupationPlanBuilder planBuilder)
    {
        _constructionRuntimeApi = constructionRuntimeApi;
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _planBuilder = planBuilder;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.ConstructionWorkPost;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (targetRef.TargetKind != TargetKind ||
            !_constructionRuntimeApi.TryGetWorkPost(targetRef.TargetId, out var workPost) ||
            workPost.CraftStationId <= 0 ||
            !_settlementsRuntimeApi.TryGetCraftStationById(workPost.CraftStationId, out var craftStation) ||
            !_settlementsRuntimeApi.TryResolveCraftStationAnchor(workPost.CraftStationId, out var anchorWorldPosition, out var anchorWorldForward))
        {
            target = null!;
            return false;
        }

        var profile = _settlementsRuntimeApi.TryGetCraftStationInteractionProfile(workPost.CraftStationId, out var interactionProfile)
            ? interactionProfile
            : CraftStationInteractionProfileRegistry.GetDefaultProfile();

        var plan = _planBuilder.BuildPlan(
            new OccupationAnchorPose(anchorWorldPosition, anchorWorldForward),
            profile.ApproachDistance,
            profile.NavigationStopDistance,
            profile.EngageRadius,
            profile.SustainRadius);

        target = new OccupationTarget(
            new OccupationTargetRef(TargetKind, workPost.Id),
            $"Construction workbench slot #{workPost.Id}",
            0,
            null,
            plan,
            OccupationExecutionProfile.ConstructionWork(craftStation.Interactable));

        return true;
    }
}
