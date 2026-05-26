using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool.WorldObjects;

public static class RegistryPlayerToolWorldObjectTargeting
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
