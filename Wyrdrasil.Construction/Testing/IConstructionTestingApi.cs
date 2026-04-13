using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Testing;

public interface IConstructionTestingApi
{
    bool TryPlaceBlueprintInstantly(PlaceBlueprintInstantRequest request, out int placedPieceCount, out string failureReason);
    bool TryForceCompleteNextPiece(int projectId, out int pieceId, out string failureReason);
    bool TryForceCompleteProject(int projectId, out int builtPieceCount, out string failureReason);
    bool TryResetProject(int projectId, out string failureReason);
    bool TryValidateProject(int projectId, out ConstructionValidationReport report, out string failureReason);
    bool TryDumpProjectState(int projectId, out string dump, out string failureReason);
    bool ToggleVerboseLogging(out bool isEnabled);
}
