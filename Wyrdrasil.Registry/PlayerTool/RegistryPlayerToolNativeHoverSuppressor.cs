using System;
using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolNativeHoverSuppressor
{
    public struct BuildRaycastSuppressionState
    {
        public bool IsActive;
        public bool HasPlaceRayMask;
        public bool HasRemoveRayMask;
        public object? OriginalPlaceRayMask;
        public object? OriginalRemoveRayMask;
    }

    private static bool _cacheInitialized;
    private static FieldInfo? _hoveringField;
    private static FieldInfo? _hoveringCreatureField;
    private static FieldInfo? _hoveringPieceField;
    private static FieldInfo? _hoveringHoverableField;
    private static FieldInfo? _hoveringInteractableField;
    private static FieldInfo? _placeRayMaskField;
    private static FieldInfo? _removeRayMaskField;

    public static bool TrySuppressNativeUpdateHover(Player? player)
    {
        if (!TryGetSuppressingTargetCharacter(player, out var targetCharacter))
        {
            return false;
        }

        ForceHoverToTargetCharacter(player!, targetCharacter);
        return true;
    }

    public static bool BeginBuildRaycastSuppression(Player? player, out BuildRaycastSuppressionState state)
    {
        state = default;

        if (!TryGetSuppressingTargetCharacter(player, out var targetCharacter))
        {
            return false;
        }

        EnsureCacheInitialized();

        state.IsActive = true;

        if (_placeRayMaskField != null && TryGetFieldValue(_placeRayMaskField, player!, out var placeRayMask))
        {
            state.HasPlaceRayMask = true;
            state.OriginalPlaceRayMask = placeRayMask;
            TrySetZeroMask(_placeRayMaskField, player!);
        }

        if (_removeRayMaskField != null && TryGetFieldValue(_removeRayMaskField, player!, out var removeRayMask))
        {
            state.HasRemoveRayMask = true;
            state.OriginalRemoveRayMask = removeRayMask;
            TrySetZeroMask(_removeRayMaskField, player!);
        }

        ForceHoverToTargetCharacter(player!, targetCharacter);
        return true;
    }

    public static void EndBuildRaycastSuppression(Player? player, BuildRaycastSuppressionState state)
    {
        if (player == null || !state.IsActive)
        {
            return;
        }

        EnsureCacheInitialized();

        if (state.HasPlaceRayMask && _placeRayMaskField != null)
        {
            TrySetFieldValue(_placeRayMaskField, player, state.OriginalPlaceRayMask);
        }

        if (state.HasRemoveRayMask && _removeRayMaskField != null)
        {
            TrySetFieldValue(_removeRayMaskField, player, state.OriginalRemoveRayMask);
        }
    }

    public static void SuppressNativeHoverBehindTargetCharacter(Player? player)
    {
        if (!TryGetSuppressingTargetCharacter(player, out var targetCharacter))
        {
            return;
        }

        ForceHoverToTargetCharacter(player!, targetCharacter);
    }

    private static bool TryGetSuppressingTargetCharacter(Player? player, out Character targetCharacter)
    {
        targetCharacter = null!;

        if (player == null || player != Player.m_localPlayer)
        {
            return false;
        }

        if (!RegistryPlayerToolSelectionService.IsRegistryToolActive(player))
        {
            return false;
        }

        return RegistryPlayerToolWorldTargeting.TryGetTargetCharacter(out targetCharacter);
    }

    private static void ForceHoverToTargetCharacter(Player player, Character targetCharacter)
    {
        EnsureCacheInitialized();

        SetFieldValue(player, _hoveringPieceField, null);
        SetFieldValue(player, _hoveringField, targetCharacter.gameObject);
        SetFieldValue(player, _hoveringCreatureField, targetCharacter);
        SetFieldValue(player, _hoveringHoverableField, null);
        SetFieldValue(player, _hoveringInteractableField, null);
    }

    private static void EnsureCacheInitialized()
    {
        if (_cacheInitialized)
        {
            return;
        }

        _cacheInitialized = true;

        const BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        _hoveringField = typeof(Player).GetField("m_hovering", instanceFlags);
        _hoveringCreatureField = typeof(Player).GetField("m_hoveringCreature", instanceFlags);
        _hoveringPieceField = typeof(Player).GetField("m_hoveringPiece", instanceFlags);
        _hoveringHoverableField = typeof(Player).GetField("m_hoveringHoverable", instanceFlags);
        _hoveringInteractableField = typeof(Player).GetField("m_hoveringInteractable", instanceFlags);
        _placeRayMaskField = typeof(Player).GetField("m_placeRayMask", instanceFlags);
        _removeRayMaskField = typeof(Player).GetField("m_removeRayMask", instanceFlags);
    }

    private static bool TryGetFieldValue(FieldInfo field, object instance, out object? value)
    {
        try
        {
            value = field.GetValue(instance);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static void SetFieldValue(object instance, FieldInfo? field, object? value)
    {
        if (field == null)
        {
            return;
        }

        TrySetFieldValue(field, instance, value);
    }

    private static bool TrySetFieldValue(FieldInfo field, object instance, object? value)
    {
        try
        {
            if (value != null && !field.FieldType.IsInstanceOfType(value))
            {
                return false;
            }

            field.SetValue(instance, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TrySetZeroMask(FieldInfo field, object instance)
    {
        try
        {
            if (field.FieldType == typeof(int))
            {
                field.SetValue(instance, 0);
                return;
            }

            if (field.FieldType == typeof(LayerMask))
            {
                var zeroMask = new LayerMask { value = 0 };
                field.SetValue(instance, zeroMask);
            }
        }
        catch
        {
            // Optional version-specific optimization. If the field shape differs,
            // the Registry still keeps its own target routing and simply lets the
            // vanilla raycast path behave normally for this frame.
        }
    }
}
