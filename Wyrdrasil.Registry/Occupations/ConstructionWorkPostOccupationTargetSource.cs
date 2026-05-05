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
    private readonly OccupationTargetFactory _targetFactory;

    public ConstructionWorkPostOccupationTargetSource(
        IConstructionRuntimeApi constructionRuntimeApi,
        ISettlementsRuntimeApi settlementsRuntimeApi,
        OccupationTargetFactory targetFactory)
    {
        _constructionRuntimeApi = constructionRuntimeApi;
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _targetFactory = targetFactory;
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

        var anchorDefinition = OccupationAnchorDefinition.FromPose(
            OccupationAnchorAttachmentKind.WorkPoint,
            anchorWorldPosition,
            anchorWorldForward,
            profile.ApproachDistance,
            profile.NavigationStopDistance,
            profile.EngageRadius,
            profile.SustainRadius,
            approachProfile: OccupationAnchorApproachProfile.WorkPointDefault);

        target = _targetFactory.CreateAnchoredTarget(
            new OccupationTargetRef(TargetKind, workPost.Id),
            $"Construction workbench slot #{workPost.Id}",
            0,
            null,
            anchorDefinition,
            OccupationExecutionProfile.ConstructionWork(craftStation.Interactable, anchorDefinition));

        return true;
    }
}
