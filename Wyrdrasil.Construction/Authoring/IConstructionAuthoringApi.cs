using System.Collections.Generic;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Authoring;

public interface IConstructionAuthoringApi
{
    bool TryCaptureBlueprint(ConstructionCaptureRequest request, out StructureBlueprintData blueprint, out string failureReason);
    bool TryCaptureBlueprintFromZone(ConstructionZoneCaptureRequest request, out StructureBlueprintData blueprint, out string failureReason);
    bool TryRegisterBlueprint(StructureBlueprintData blueprint, out string failureReason);
    bool TryCreateProject(CreateConstructionProjectRequest request, out ConstructionProjectData project, out string failureReason);
    IReadOnlyList<StructureBlueprintData> GetBlueprints();
}
