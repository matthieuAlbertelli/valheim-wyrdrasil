using UnityEngine;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Occupations;

public sealed class ConstructionWorkPostOccupationTargetSource : IOccupationTargetSource
{
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly CraftStationService _craftStationService;
    private readonly AnchorOccupationPlanBuilder _planBuilder;

    public ConstructionWorkPostOccupationTargetSource(
        IConstructionRuntimeApi constructionRuntimeApi,
        CraftStationService craftStationService,
        AnchorOccupationPlanBuilder planBuilder)
    {
        _constructionRuntimeApi = constructionRuntimeApi;
        _craftStationService = craftStationService;
        _planBuilder = planBuilder;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.ConstructionWorkPost;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (targetRef.TargetKind != TargetKind ||
            !_constructionRuntimeApi.TryGetWorkPost(targetRef.TargetId, out var workPost) ||
            workPost.CraftStationId <= 0 ||
            !_craftStationService.TryGetCraftStationById(workPost.CraftStationId, out var craftStation) ||
            !craftStation.TryResolveWorldAnchor(out var anchorWorldPosition, out var anchorWorldForward))
        {
            target = null!;
            return false;
        }

        var profile = _craftStationService.TryGetInteractionProfile(craftStation, out var interactionProfile)
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
