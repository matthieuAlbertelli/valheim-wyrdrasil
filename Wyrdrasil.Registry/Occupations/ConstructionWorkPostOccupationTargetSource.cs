using UnityEngine;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Occupations;

public sealed class ConstructionWorkPostOccupationTargetSource : IOccupationTargetSource
{
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly AnchorOccupationPlanBuilder _planBuilder;

    public ConstructionWorkPostOccupationTargetSource(
        IConstructionRuntimeApi constructionRuntimeApi,
        AnchorOccupationPlanBuilder planBuilder)
    {
        _constructionRuntimeApi = constructionRuntimeApi;
        _planBuilder = planBuilder;
    }

    public OccupationTargetKind TargetKind => OccupationTargetKind.ConstructionWorkPost;

    public bool TryResolve(OccupationTargetRef targetRef, out OccupationTarget target)
    {
        if (targetRef.TargetKind != TargetKind ||
            !_constructionRuntimeApi.TryGetWorkPost(targetRef.TargetId, out var workPost))
        {
            target = null!;
            return false;
        }

        var profile = CraftStationInteractionProfileRegistry.GetDefaultProfile();
        var facingDirection = workPost.WorldRotation * Vector3.forward;
        var plan = _planBuilder.BuildPlan(
            new OccupationAnchorPose(workPost.WorldPosition, facingDirection),
            profile.ApproachDistance,
            profile.NavigationStopDistance,
            profile.EngageRadius,
            profile.SustainRadius);

        target = new OccupationTarget(
            new OccupationTargetRef(TargetKind, workPost.Id),
            $"Construction work post #{workPost.Id}",
            0,
            null,
            plan,
            OccupationExecutionProfile.ConstructionWork());

        return true;
    }
}
