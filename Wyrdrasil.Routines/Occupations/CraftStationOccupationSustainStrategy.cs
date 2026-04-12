using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;
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
        var isNearWorkbench = executionService.IsNearEngagePosition(character, target, target.Plan.SustainRadius);
        var navigationActive = executionService.IsNavigationActive(character);

        if (!isNearWorkbench || navigationActive)
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, $"Sustain abort isNearWorkbench={isNearWorkbench} navigationActive={navigationActive} engageDistance={executionService.GetHorizontalDistance(character, target.Plan.EngagePosition):0.00}");
            return OccupationSustainResult.Abort;
        }

        if (character is not WyrdrasilVikingNpc viking)
        {
            return OccupationSustainResult.Abort;
        }

        if (!viking.IsInWorkbenchPose())
        {
            WyrdrasilOccupationDebug.LogCraftStation(character, "Sustain abort pose lost");
            return OccupationSustainResult.Abort;
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
