using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Wyrdrasil.Construction.Services;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool;

/// <summary>
/// Reveals Wyrdrasil-owned world markers while the player tool is in Inspect mode.
///
/// The service deliberately uses a small convention-based adapter instead of a hard-coded
/// list of today's marker types. New Wyrdrasil markers can participate automatically when
/// they expose one or more of these methods:
/// - SetInspectionRevealVisible(bool)
/// - SetVisualizationVisible(bool)
/// - SetVisualizationVisible(bool, bool)
/// - SetHighlighted(bool)
/// - SetSelected(bool)
///
/// This keeps Inspect as a stable cross-module reveal mode without coupling it to every
/// settlement/soul/construction marker implementation.
/// </summary>
public sealed class RegistryPlayerToolInspectionRevealService
{
    private const float RefreshIntervalSeconds = 0.35f;

    private readonly ConstructionProjectMarkerService _constructionProjectMarkerService;
    private readonly ConstructionProjectGhostService _constructionProjectGhostService;
    private readonly Dictionary<int, MarkerRevealBinding> _bindingsByInstanceId = new();
    private bool _isVisible;
    private float _nextRefreshTime;

    public bool IsVisible => _isVisible;

    public RegistryPlayerToolInspectionRevealService(
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionProjectGhostService constructionProjectGhostService)
    {
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _constructionProjectGhostService = constructionProjectGhostService;
    }

    public void SetRevealVisible(bool visible)
    {
        if (_isVisible == visible)
        {
            if (visible)
            {
                ApplyRevealToExistingBindings();
                RefreshBindingsIfDue();
            }

            return;
        }

        _isVisible = visible;
        _constructionProjectMarkerService.SetInspectionRevealVisible(visible);
        _constructionProjectGhostService.SetInspectionRevealVisible(visible);

        if (!visible)
        {
            RestoreAndClearBindings();
            return;
        }

        _nextRefreshTime = 0f;
        RefreshBindings();
    }

    public void Update()
    {
        if (!_isVisible)
        {
            return;
        }

        RefreshBindingsIfDue();
    }

    private void ApplyRevealToExistingBindings()
    {
        foreach (var binding in _bindingsByInstanceId.Values)
        {
            binding.ApplyReveal();
        }
    }

    private void RefreshBindingsIfDue()
    {
        if (Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        RefreshBindings();
    }

    private void RefreshBindings()
    {
        _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;

        foreach (var staleId in _bindingsByInstanceId
                     .Where(pair => pair.Value.IsStale)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _bindingsByInstanceId.Remove(staleId);
        }

        foreach (var component in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (component == null)
            {
                continue;
            }

            var instanceId = component.GetInstanceID();
            if (_bindingsByInstanceId.ContainsKey(instanceId))
            {
                continue;
            }

            if (MarkerRevealBinding.TryCreate(component, out var binding))
            {
                _bindingsByInstanceId[instanceId] = binding;
                binding.ApplyReveal();
            }
        }

        ApplyRevealToExistingBindings();
    }

    private void RestoreAndClearBindings()
    {
        foreach (var binding in _bindingsByInstanceId.Values)
        {
            binding.Restore();
        }

        _bindingsByInstanceId.Clear();
    }

    private sealed class MarkerRevealBinding
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly MonoBehaviour _component;
        private readonly MethodInfo? _setInspectionRevealVisibleMethod;
        private readonly MethodInfo? _setVisualizationVisibleSingleMethod;
        private readonly MethodInfo? _setVisualizationVisibleAssignedMethod;
        private readonly MethodInfo? _setHighlightedMethod;
        private readonly MethodInfo? _setSelectedMethod;
        private readonly bool _originalVisualizationVisible;
        private readonly bool _originalAssigned;
        private readonly bool _originalHighlighted;
        private readonly bool _originalSelected;

        private MarkerRevealBinding(
            MonoBehaviour component,
            MethodInfo? setInspectionRevealVisibleMethod,
            MethodInfo? setVisualizationVisibleSingleMethod,
            MethodInfo? setVisualizationVisibleAssignedMethod,
            MethodInfo? setHighlightedMethod,
            MethodInfo? setSelectedMethod)
        {
            _component = component;
            _setInspectionRevealVisibleMethod = setInspectionRevealVisibleMethod;
            _setVisualizationVisibleSingleMethod = setVisualizationVisibleSingleMethod;
            _setVisualizationVisibleAssignedMethod = setVisualizationVisibleAssignedMethod;
            _setHighlightedMethod = setHighlightedMethod;
            _setSelectedMethod = setSelectedMethod;
            _originalVisualizationVisible = ResolveOriginalVisibility(component);
            _originalAssigned = ResolveBoolField(component, "_isAssigned", false);
            _originalHighlighted = ResolveBoolField(component, "_isHighlighted", false);
            _originalSelected = ResolveBoolField(component, "_isSelected", false);
        }

        public bool IsStale => _component == null;

        public static bool TryCreate(MonoBehaviour component, out MarkerRevealBinding binding)
        {
            binding = null!;

            var type = component.GetType();
            if (!IsWyrdrasilMarkerType(type))
            {
                return false;
            }

            var setInspectionRevealVisibleMethod = FindBooleanMethod(type, "SetInspectionRevealVisible", 1);
            var setVisualizationVisibleSingleMethod = FindBooleanMethod(type, "SetVisualizationVisible", 1);
            var setVisualizationVisibleAssignedMethod = FindBooleanMethod(type, "SetVisualizationVisible", 2);
            var setHighlightedMethod = FindBooleanMethod(type, "SetHighlighted", 1);
            var setSelectedMethod = FindBooleanMethod(type, "SetSelected", 1);

            if (setInspectionRevealVisibleMethod == null &&
                setVisualizationVisibleSingleMethod == null &&
                setVisualizationVisibleAssignedMethod == null &&
                setHighlightedMethod == null &&
                setSelectedMethod == null)
            {
                return false;
            }

            binding = new MarkerRevealBinding(
                component,
                setInspectionRevealVisibleMethod,
                setVisualizationVisibleSingleMethod,
                setVisualizationVisibleAssignedMethod,
                setHighlightedMethod,
                setSelectedMethod);

            return true;
        }

        public void ApplyReveal()
        {
            if (_component == null)
            {
                return;
            }

            if (TryInvoke(_setInspectionRevealVisibleMethod, true))
            {
                return;
            }

            TryInvoke(_setVisualizationVisibleAssignedMethod, true, _originalAssigned);
            TryInvoke(_setVisualizationVisibleSingleMethod, true);
            TryInvoke(_setHighlightedMethod, true);
            TryInvoke(_setSelectedMethod, true);
        }

        public void Restore()
        {
            if (_component == null)
            {
                return;
            }

            if (TryInvoke(_setInspectionRevealVisibleMethod, false))
            {
                return;
            }

            TryInvoke(_setHighlightedMethod, _originalHighlighted);
            TryInvoke(_setSelectedMethod, _originalSelected);
            TryInvoke(_setVisualizationVisibleAssignedMethod, _originalVisualizationVisible, _originalAssigned);
            TryInvoke(_setVisualizationVisibleSingleMethod, _originalVisualizationVisible);
        }

        private static bool IsWyrdrasilMarkerType(Type type)
        {
            var fullName = type.FullName ?? string.Empty;
            if (!fullName.StartsWith("Wyrdrasil.", StringComparison.Ordinal))
            {
                return false;
            }

            if (type.Name.StartsWith("Wyrdrasil", StringComparison.Ordinal) &&
                type.Name.EndsWith("Marker", StringComparison.Ordinal))
            {
                return true;
            }

            return FindBooleanMethod(type, "SetInspectionRevealVisible", 1) != null;
        }

        private static MethodInfo? FindBooleanMethod(Type type, string methodName, int booleanParameterCount)
        {
            foreach (var method in type.GetMethods(InstanceFlags))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != booleanParameterCount)
                {
                    continue;
                }

                var allBoolean = true;
                foreach (var parameter in parameters)
                {
                    if (parameter.ParameterType != typeof(bool))
                    {
                        allBoolean = false;
                        break;
                    }
                }

                if (allBoolean)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool ResolveOriginalVisibility(MonoBehaviour component)
        {
            if (TryResolveBoolField(component, "_isVisible", out var isVisible))
            {
                return isVisible;
            }

            var renderers = component.GetComponentsInChildren<Renderer>(true);
            return renderers.Any(renderer => renderer != null && renderer.enabled);
        }

        private static bool ResolveBoolField(MonoBehaviour component, string fieldName, bool fallback)
        {
            return TryResolveBoolField(component, fieldName, out var value)
                ? value
                : fallback;
        }

        private static bool TryResolveBoolField(MonoBehaviour component, string fieldName, out bool value)
        {
            value = false;
            for (var type = component.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, InstanceFlags);
                if (field == null || field.FieldType != typeof(bool))
                {
                    continue;
                }

                value = (bool)field.GetValue(component);
                return true;
            }

            return false;
        }

        private bool TryInvoke(MethodInfo? method, params object[] arguments)
        {
            if (method == null || _component == null)
            {
                return false;
            }

            try
            {
                method.Invoke(_component, arguments);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
