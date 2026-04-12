using Wyrdrasil.Registry.Diagnostics;
using Wyrdrasil.Routines.Components;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Routines.Occupations;

public sealed class CraftStationOccupationSustainStrategy : IOccupationSustainStrategy
{
    public string StrategyId => OccupationExecutionProfile.CraftStationStrategyId;
    public float TickIntervalSeconds => 0.25f;

    public OccupationSustainResult Sustain(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        if (executionService.IsNavigationActive(character))
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, "Sustain abort navigationActive=True");
            return OccupationSustainResult.Abort;
        }

        if (!character.TryGetComponent<WyrdrasilEngagedPoseController>(out var controller) || !controller.IsEngaged)
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, "Sustain abort engagedPoseControllerMissing");
            return OccupationSustainResult.Abort;
        }

        if (character is WyrdrasilVikingNpc viking && !viking.IsInWorkbenchPose())
        {
            _ = viking.TryEnterWorkbenchPose();
        }

        return OccupationSustainResult.Continue;
    }

    public void Release(OccupationExecutionService executionService, RegisteredNpcData resident, Character character, OccupationTarget target, OccupationSession session)
    {
        if (character is WyrdrasilVikingNpc viking)
        {
            viking.TryExitWorkbenchPose();
        }
    }
}
