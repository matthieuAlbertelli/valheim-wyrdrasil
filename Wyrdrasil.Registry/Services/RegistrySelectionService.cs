using System;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistrySelectionService
{
    private static readonly RegistryCategory[] Categories = (RegistryCategory[])Enum.GetValues(typeof(RegistryCategory));

    private static readonly RegistryActionType[] ZoneActions =
    {
        RegistryActionType.CreateTavernZone,
        RegistryActionType.CreateBedroomZone,
        RegistryActionType.CreateNavigationWaypoint,
        RegistryActionType.ConnectNavigationWaypoints,
        RegistryActionType.DeleteNavigationWaypoint,
        RegistryActionType.DeleteZone
    };

    private static readonly RegistryActionType[] SlotActions =
    {
        RegistryActionType.CreateInnkeeperSlot,
        RegistryActionType.DesignateSeatFurniture,
        RegistryActionType.DesignateBedFurniture,
        RegistryActionType.DeleteSlot,
        RegistryActionType.DeleteDesignatedSeat,
        RegistryActionType.DeleteDesignatedBed
    };

    private static readonly RegistryActionType[] ResidentActions =
    {
        RegistryActionType.RegisterNpc,
        RegistryActionType.AssignInnkeeperRole,
        RegistryActionType.AssignSeat,
        RegistryActionType.AssignBed,
        RegistryActionType.ClearTargetInnkeeperSlotAssignment,
        RegistryActionType.ClearTargetSeatAssignment,
        RegistryActionType.ClearTargetBedAssignment,
        RegistryActionType.ForceAssignResident,
        RegistryActionType.DespawnTargetResident,
        RegistryActionType.RespawnAssignedResident,
        RegistryActionType.SpawnTestViking
    };

    private static readonly RegistryActionType[] DiagnosticActions =
    {
        RegistryActionType.InspectTargetNpcAi,
        RegistryActionType.SimulateNoon,
        RegistryActionType.SimulateNight,
        RegistryActionType.ClearTimeSimulation,
        RegistryActionType.FlushRegistryState,
        RegistryActionType.None
    };

    private readonly RegistryToolState _state;

    public RegistrySelectionService(RegistryToolState state)
    {
        _state = state;
        EnsureSelectionIsValid();
    }

    public void SelectNextCategory()
    {
        var currentIndex = Array.IndexOf(Categories, _state.SelectedCategory);
        var nextIndex = (currentIndex + 1) % Categories.Length;
        var nextCategory = Categories[nextIndex];
        _state.SetSelectedCategory(nextCategory);
        _state.SetSelectedAction(GetActionsForCategory(nextCategory)[0]);
    }

    public void SelectNextAction()
    {
        var availableActions = GetActionsForCategory(_state.SelectedCategory);
        var currentIndex = Array.IndexOf(availableActions, _state.SelectedAction);
        if (currentIndex < 0)
        {
            _state.SetSelectedAction(availableActions[0]);
            return;
        }

        _state.SetSelectedAction(availableActions[(currentIndex + 1) % availableActions.Length]);
    }

    private void EnsureSelectionIsValid()
    {
        var availableActions = GetActionsForCategory(_state.SelectedCategory);
        if (Array.IndexOf(availableActions, _state.SelectedAction) < 0)
        {
            _state.SetSelectedAction(availableActions[0]);
        }
    }

    private static RegistryActionType[] GetActionsForCategory(RegistryCategory category)
    {
        return category switch
        {
            RegistryCategory.Zones => ZoneActions,
            RegistryCategory.Slots => SlotActions,
            RegistryCategory.Residents => ResidentActions,
            RegistryCategory.Diagnostics => DiagnosticActions,
            _ => DiagnosticActions
        };
    }
}
