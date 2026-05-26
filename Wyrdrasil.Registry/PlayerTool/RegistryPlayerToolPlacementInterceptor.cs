using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolPlacementInterceptor
{
    private static int _lastConsumedFrame = -1;

    private static Type? _inventoryGuiType;
    private static MethodInfo? _inventoryGuiIsVisibleMethod;
    private static Type? _menuType;
    private static MethodInfo? _menuIsVisibleMethod;
    private static Type? _textInputType;
    private static MethodInfo? _textInputIsVisibleMethod;
    private static Type? _storeGuiType;
    private static MethodInfo? _storeGuiIsVisibleMethod;
    private static Type? _eventSystemType;
    private static PropertyInfo? _eventSystemCurrentProperty;
    private static MethodInfo? _eventSystemIsPointerOverGameObjectMethod;
    private static bool _reflectionCacheInitialized;

    public static bool TryConsumePlacementAction(Player? player)
    {
        if (player == null || player != Player.m_localPlayer)
        {
            return false;
        }

        // Keep the Registry as close as possible to Valheim's native tool flow:
        // on normal frames we do nothing. We only intervene while the player is
        // actually pressing the primary/secondary mouse buttons.
        var primaryDown = Input.GetMouseButtonDown(0);
        var primaryHeld = Input.GetMouseButton(0);
        var secondaryDown = Input.GetMouseButtonDown(1);
        var secondaryHeld = Input.GetMouseButton(1);

        if (!primaryHeld && !secondaryHeld && !primaryDown && !secondaryDown)
        {
            return false;
        }

        if (!RegistryPlayerToolSelectionService.IsRegistryToolActive(player))
        {
            return false;
        }

        // If the click is happening over UI or while gameplay input is blocked,
        // do not execute Wyrdrasil actions, but still skip native placement so a
        // panel click cannot leak into the world.
        if (IsGameplayInputBlocked())
        {
            _lastConsumedFrame = Time.frameCount;
            return true;
        }

        if (_lastConsumedFrame == Time.frameCount)
        {
            return true;
        }

        if (RegistryPlayerToolActionRouter.IsConstructionPreviewActive)
        {
            _lastConsumedFrame = Time.frameCount;
            return true;
        }

        if (primaryDown)
        {
            RegistryPlayerToolActionRouter.TryExecuteSelectedPrimaryAction(player);
            _lastConsumedFrame = Time.frameCount;
            return true;
        }

        if (secondaryDown)
        {
            RegistryPlayerToolActionRouter.TryExecuteSelectedSecondaryAction(player);
            _lastConsumedFrame = Time.frameCount;
            return true;
        }

        // While either mouse button is held with the Registry equipped, do not let
        // Valheim's vanilla build/remove path process the pseudo-piece. This also
        // avoids falling through to the native RemovePiece path for held right-clicks,
        // which caused a visible one-frame hitch when blocked later.
        _lastConsumedFrame = Time.frameCount;
        return true;
    }

    public static bool TryConsumePrimaryAction(Player? player)
    {
        return TryConsumePlacementAction(player);
    }

    public static bool TryConsumeNativeRemoveAction(Player? player)
    {
        if (player == null || player != Player.m_localPlayer)
        {
            return false;
        }

        if (!RegistryPlayerToolSelectionService.IsRegistryToolActive(player))
        {
            return false;
        }

        // Safety net only. The normal right-click path is handled earlier in
        // UpdatePlacement. If Valheim still reaches RemovePiece while the Registry
        // is equipped, block it immediately and do no gameplay routing here. Doing
        // work at this late point caused short hitches because Valheim had already
        // entered the native destruction flow.
        _lastConsumedFrame = Time.frameCount;
        return true;
    }

    public static bool IsGameplayInputBlocked()
    {
        if (Time.timeScale <= 0f)
        {
            return true;
        }

        EnsureReflectionCacheInitialized();

        if (InvokeCachedStaticBool(_inventoryGuiIsVisibleMethod) ||
            InvokeCachedStaticBool(_menuIsVisibleMethod) ||
            InvokeCachedStaticBool(_textInputIsVisibleMethod) ||
            InvokeCachedStaticBool(_storeGuiIsVisibleMethod))
        {
            return true;
        }

        return IsPointerOverUnityUi();
    }

    private static void EnsureReflectionCacheInitialized()
    {
        if (_reflectionCacheInitialized)
        {
            return;
        }

        _reflectionCacheInitialized = true;

        _inventoryGuiType = AccessTools.TypeByName("InventoryGui");
        _inventoryGuiIsVisibleMethod = FindStaticBoolMethod(_inventoryGuiType, "IsVisible");

        _menuType = AccessTools.TypeByName("Menu");
        _menuIsVisibleMethod = FindStaticBoolMethod(_menuType, "IsVisible");

        _textInputType = AccessTools.TypeByName("TextInput");
        _textInputIsVisibleMethod = FindStaticBoolMethod(_textInputType, "IsVisible");

        _storeGuiType = AccessTools.TypeByName("StoreGui");
        _storeGuiIsVisibleMethod = FindStaticBoolMethod(_storeGuiType, "IsVisible");

        _eventSystemType = AccessTools.TypeByName("UnityEngine.EventSystems.EventSystem");
        if (_eventSystemType != null)
        {
            _eventSystemCurrentProperty = _eventSystemType.GetProperty(
                "current",
                BindingFlags.Public | BindingFlags.Static);

            _eventSystemIsPointerOverGameObjectMethod = _eventSystemType.GetMethod(
                "IsPointerOverGameObject",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
        }
    }

    private static MethodInfo? FindStaticBoolMethod(Type? type, string methodName)
    {
        if (type == null)
        {
            return null;
        }

        var method = type.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
            null,
            Type.EmptyTypes,
            null);

        return method != null && method.ReturnType == typeof(bool)
            ? method
            : null;
    }

    private static bool InvokeCachedStaticBool(MethodInfo? method)
    {
        if (method == null)
        {
            return false;
        }

        try
        {
            return method.Invoke(null, null) is true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPointerOverUnityUi()
    {
        if (_eventSystemCurrentProperty == null || _eventSystemIsPointerOverGameObjectMethod == null)
        {
            return false;
        }

        try
        {
            var current = _eventSystemCurrentProperty.GetValue(null, null);
            return current != null && _eventSystemIsPointerOverGameObjectMethod.Invoke(current, null) is true;
        }
        catch
        {
            return false;
        }
    }
}
