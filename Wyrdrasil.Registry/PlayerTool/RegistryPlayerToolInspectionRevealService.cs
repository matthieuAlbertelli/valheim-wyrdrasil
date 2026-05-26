using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Registry.PlayerTool.Inspection;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Souls.Runtime;
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
    private readonly RegistryPlayerToolInspectionLinkService _inspectionLinkService;
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly ISoulsRuntimeApi _soulsRuntimeApi;
    private readonly Dictionary<int, MarkerRevealBinding> _bindingsByInstanceId = new();
    private bool _isVisible;
    private float _nextRefreshTime;

    public bool IsVisible => _isVisible;

    public RegistryPlayerToolInspectionRevealService(
        ConstructionProjectMarkerService constructionProjectMarkerService,
        ConstructionProjectGhostService constructionProjectGhostService,
        RegistryPlayerToolInspectionLinkService inspectionLinkService,
        ISettlementsRuntimeApi settlementsRuntimeApi,
        ISoulsRuntimeApi soulsRuntimeApi)
    {
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _constructionProjectGhostService = constructionProjectGhostService;
        _inspectionLinkService = inspectionLinkService;
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _soulsRuntimeApi = soulsRuntimeApi;
    }

    public void SetRevealVisible(bool visible)
    {
        if (_isVisible == visible)
        {
            if (visible)
            {
                ApplyRevealToExistingBindings();
                RefreshBindingsIfDue();
                _inspectionLinkService.Update();
            }

            return;
        }

        _isVisible = visible;
        _constructionProjectMarkerService.SetInspectionRevealVisible(visible);
        _constructionProjectGhostService.SetInspectionRevealVisible(visible);
        _inspectionLinkService.SetVisible(visible);

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
        _inspectionLinkService.Update();
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

            if (MarkerRevealBinding.TryCreate(component, _settlementsRuntimeApi, _soulsRuntimeApi, out var binding))
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
        private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
        private readonly ISoulsRuntimeApi _soulsRuntimeApi;
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
            MethodInfo? setSelectedMethod,
            ISettlementsRuntimeApi settlementsRuntimeApi,
            ISoulsRuntimeApi soulsRuntimeApi)
        {
            _component = component;
            _setInspectionRevealVisibleMethod = setInspectionRevealVisibleMethod;
            _setVisualizationVisibleSingleMethod = setVisualizationVisibleSingleMethod;
            _setVisualizationVisibleAssignedMethod = setVisualizationVisibleAssignedMethod;
            _setHighlightedMethod = setHighlightedMethod;
            _setSelectedMethod = setSelectedMethod;
            _settlementsRuntimeApi = settlementsRuntimeApi;
            _soulsRuntimeApi = soulsRuntimeApi;
            _originalVisualizationVisible = ResolveOriginalVisibility(component);
            _originalAssigned = ResolveRuntimeAssigned(component) ?? ResolveBoolField(component, "_isAssigned", false);
            _originalHighlighted = ResolveBoolField(component, "_isHighlighted", false);
            _originalSelected = ResolveBoolField(component, "_isSelected", false);
        }

        public bool IsStale => _component == null;

        public static bool TryCreate(
            MonoBehaviour component,
            ISettlementsRuntimeApi settlementsRuntimeApi,
            ISoulsRuntimeApi soulsRuntimeApi,
            out MarkerRevealBinding binding)
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
                setSelectedMethod,
                settlementsRuntimeApi,
                soulsRuntimeApi);

            return true;
        }

        public void ApplyReveal()
        {
            if (_component == null)
            {
                return;
            }

            var assigned = ResolveRuntimeAssigned(_component) ?? _originalAssigned;

            // Prefer the explicit inspect visualization overload when it exists.
            // Calling the single-boolean method afterwards can overwrite the assigned color
            // on some markers, so the two-boolean form is authoritative.
            if (!TryInvoke(_setVisualizationVisibleAssignedMethod, true, assigned))
            {
                if (!TryInvoke(_setInspectionRevealVisibleMethod, true))
                {
                    TryInvoke(_setVisualizationVisibleSingleMethod, true);
                }
            }

            TryInvoke(_setHighlightedMethod, true);
            TryInvoke(_setSelectedMethod, true);
        }

        public void Restore()
        {
            if (_component == null)
            {
                return;
            }

            TryInvoke(_setHighlightedMethod, _originalHighlighted);
            TryInvoke(_setSelectedMethod, _originalSelected);

            var assigned = ResolveRuntimeAssigned(_component) ?? _originalAssigned;
            if (!TryInvoke(_setVisualizationVisibleAssignedMethod, _originalVisualizationVisible, assigned))
            {
                if (!TryInvoke(_setInspectionRevealVisibleMethod, false))
                {
                    TryInvoke(_setVisualizationVisibleSingleMethod, _originalVisualizationVisible);
                }
            }
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

        private bool? ResolveRuntimeAssigned(MonoBehaviour component)
        {
            if (TryResolveIntField(component, "_bedId", out var bedId))
            {
                return IsBedAssignedByRuntimeState(bedId) || IsBedAssignedByResident(bedId);
            }

            if (TryResolveIntField(component, "_craftStationId", out var craftStationId) ||
                TryResolveIntProperty(component, "CraftStationId", out craftStationId))
            {
                return IsCraftStationAssignedByRuntimeState(craftStationId) || IsCraftStationAssignedByResident(craftStationId);
            }

            if (TryResolveIntField(component, "_seatId", out var seatId))
            {
                return IsSeatAssignedByRuntimeState(seatId) || IsSeatAssignedByResident(seatId);
            }

            if (TryResolveIntProperty(component, "SlotId", out var slotId))
            {
                return IsSlotAssignedByRuntimeState(slotId) || IsSlotAssignedByResident(slotId);
            }

            return null;
        }

        private bool IsBedAssignedByRuntimeState(int bedId)
        {
            foreach (var bed in _settlementsRuntimeApi.Beds)
            {
                if (bed.Id == bedId)
                {
                    return bed.AssignedRegisteredNpcId.HasValue;
                }
            }

            return false;
        }

        private bool IsCraftStationAssignedByRuntimeState(int craftStationId)
        {
            foreach (var station in _settlementsRuntimeApi.CraftStations)
            {
                if (station.Id == craftStationId)
                {
                    return station.AssignedRegisteredNpcId.HasValue;
                }
            }

            return false;
        }

        private bool IsSeatAssignedByRuntimeState(int seatId)
        {
            foreach (var seat in _settlementsRuntimeApi.Seats)
            {
                if (seat.Id == seatId)
                {
                    return seat.AssignedRegisteredNpcId.HasValue;
                }
            }

            return false;
        }

        private bool IsSlotAssignedByRuntimeState(int slotId)
        {
            foreach (var slot in _settlementsRuntimeApi.Slots)
            {
                if (slot.Id == slotId)
                {
                    return slot.AssignedRegisteredNpcId.HasValue;
                }
            }

            return false;
        }

        private bool IsBedAssignedByResident(int bedId)
        {
            foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
            {
                if (resident.AssignedBedId == bedId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCraftStationAssignedByResident(int craftStationId)
        {
            foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
            {
                if (resident.AssignedCraftStationId == craftStationId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSeatAssignedByResident(int seatId)
        {
            foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
            {
                if (resident.AssignedSeatId == seatId)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSlotAssignedByResident(int slotId)
        {
            foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
            {
                if (resident.AssignedSlotId == slotId)
                {
                    return true;
                }
            }

            return false;
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

        private static bool TryResolveIntField(MonoBehaviour component, string fieldName, out int value)
        {
            value = 0;
            for (var type = component.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, InstanceFlags);
                if (field == null || field.FieldType != typeof(int))
                {
                    continue;
                }

                value = (int)field.GetValue(component);
                return value > 0;
            }

            return false;
        }

        private static bool TryResolveIntProperty(MonoBehaviour component, string propertyName, out int value)
        {
            value = 0;
            for (var type = component.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(propertyName, InstanceFlags);
                if (property == null || property.PropertyType != typeof(int))
                {
                    continue;
                }

                value = (int)property.GetValue(component, null);
                return value > 0;
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
