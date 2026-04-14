using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Routines.Occupations;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Occupations;

public sealed class ConstructionWorkOccupationSustainStrategy : IOccupationSustainStrategy
{
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;

    public ConstructionWorkOccupationSustainStrategy(IConstructionRuntimeApi constructionRuntimeApi)
    {
        _constructionRuntimeApi = constructionRuntimeApi;
    }

    public string StrategyId => OccupationExecutionProfile.ConstructionWorkSustainStrategyId;
    public float TickIntervalSeconds => 0.5f;

    public OccupationSustainResult Sustain(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        if (!_constructionRuntimeApi.TryGetAssignedWorkPost(resident.Id, out var assignedWorkPost) ||
            assignedWorkPost.Id != target.Reference.TargetId)
        {
            Deactivate(resident.Id, target.Reference.TargetId);
            return OccupationSustainResult.Abort;
        }

        if (!_constructionRuntimeApi.IsProjectActive(assignedWorkPost.ProjectId))
        {
            Deactivate(resident.Id, assignedWorkPost.Id);
            return OccupationSustainResult.Complete;
        }

        if (executionService.IsNavigationActive(character) ||
            !executionService.IsNearEngagePosition(character, target, target.Plan.SustainRadius))
        {
            Deactivate(resident.Id, assignedWorkPost.Id);
            return OccupationSustainResult.Abort;
        }

        _constructionRuntimeApi.TrySetResidentWorkActive(resident.Id, assignedWorkPost.Id, true);
        return OccupationSustainResult.Continue;
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        Deactivate(resident.Id, target.Reference.TargetId);
    }

    private void Deactivate(int residentId, int workPostId)
    {
        _constructionRuntimeApi.TrySetResidentWorkActive(residentId, workPostId, false);
    }
}
