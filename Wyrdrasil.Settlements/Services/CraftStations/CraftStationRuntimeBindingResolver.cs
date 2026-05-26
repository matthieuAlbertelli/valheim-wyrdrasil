using UnityEngine;

namespace Wyrdrasil.Settlements.Services.CraftStations;

/// <summary>
/// Resolves the stable runtime object that represents a Valheim crafting station.
///
/// Valheim prefabs do not always expose their visual root on the same GameObject
/// that owns the CraftingStation component. Registry systems must therefore
/// centralize this rule instead of guessing locally in authoring, persistence,
/// markers or inspection code.
/// </summary>
public static class CraftStationRuntimeBindingResolver
{
    public static GameObject ResolveFurnitureRoot(CraftingStation craftingStation)
    {
        var piece = craftingStation.GetComponentInParent<Piece>();
        if (piece != null)
        {
            return piece.gameObject;
        }

        var nview = craftingStation.GetComponentInParent<ZNetView>();
        if (nview != null)
        {
            return nview.gameObject;
        }

        var rendererRoot = FindNearestRenderableRoot(craftingStation.transform);
        if (rendererRoot != null)
        {
            return rendererRoot.gameObject;
        }

        return craftingStation.gameObject;
    }

    public static bool RefersToSameFurniture(GameObject candidateRoot, GameObject runtimeRoot)
    {
        if (candidateRoot == null || runtimeRoot == null)
        {
            return false;
        }

        if (candidateRoot == runtimeRoot)
        {
            return true;
        }

        return candidateRoot.transform.IsChildOf(runtimeRoot.transform) ||
               runtimeRoot.transform.IsChildOf(candidateRoot.transform);
    }

    private static Transform? FindNearestRenderableRoot(Transform start)
    {
        var current = start;
        while (current != null)
        {
            if (current.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }
}
