using UnityEngine;
using Wyrdrasil.Registry.PlayerTool;

namespace Wyrdrasil.Registry.PlayerTool.Assignments;

public static class RegistryPlayerToolAssignmentWorldTargeting
{
    public static bool HasComponentAtCrosshair<T>() where T : Component
    {
        return TryGetComponentAtCrosshair<T>(out _);
    }

    public static bool TryGetComponentAtCrosshair<T>(out T component) where T : Component
    {
        component = null!;
        if (!RegistryPlayerToolWorldTargeting.TryGetRegularRaycastTarget(out var hitInfo) || hitInfo.collider == null)
        {
            return false;
        }

        component = hitInfo.collider.GetComponentInParent<T>();
        return component != null;
    }
}
