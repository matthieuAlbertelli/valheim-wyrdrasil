namespace Wyrdrasil.Registry.Patches;

// This file is intentionally NOT a Harmony patch.
//
// A previous implementation attempted to discover and patch a Valheim Piece
// highlight method by reflection. The current Valheim build used by the project
// does not expose a compatible Piece.*Highlight(bool) method, and Harmony fails
// at plugin startup when that dynamic TargetMethod is present.
//
// Keep this inert type in place so the old source file is overwritten cleanly.
internal static class WyrdrasilRegistryPlayerToolPieceHighlightPatch
{
}
