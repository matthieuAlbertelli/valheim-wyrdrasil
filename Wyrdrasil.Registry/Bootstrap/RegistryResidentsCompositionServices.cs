using Wyrdrasil.Registry.Services;
namespace Wyrdrasil.Registry.Bootstrap;

internal sealed class RegistryResidentsCompositionServices
{
    public RegistryResidentsCompositionServices(
        ResidentVisualService residentVisualService,
        ResidentPresenceService residentPresenceService,
        ResidentAssignmentService residentAssignmentService,
        RegistryResidentService residentService,
        ResidentRoutineService residentRoutineService)
    {
        ResidentVisualService = residentVisualService;
        ResidentPresenceService = residentPresenceService;
        ResidentAssignmentService = residentAssignmentService;
        ResidentService = residentService;
        ResidentRoutineService = residentRoutineService;
    }

    public ResidentVisualService ResidentVisualService { get; }
    public ResidentPresenceService ResidentPresenceService { get; }
    public ResidentAssignmentService ResidentAssignmentService { get; }
    public RegistryResidentService ResidentService { get; }
    public ResidentRoutineService ResidentRoutineService { get; }
}
