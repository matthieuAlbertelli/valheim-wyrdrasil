using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryDiagnosticsService
{
    private readonly ManualLogSource _log;

    public RegistryDiagnosticsService(ManualLogSource log)
    {
        _log = log;
    }

    public void InspectTargetNpcAiAtCrosshair()
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            _log.LogWarning("Diagnostics: no valid character is under the crosshair.");
            return;
        }

        var characterType = targetCharacter.GetType();
        _log.LogInfo("================ AI DIAGNOSTICS BEGIN ================");
        _log.LogInfo($"Target character: {targetCharacter.gameObject.name}");
        _log.LogInfo($"Character type: {characterType.FullName}");
        _log.LogInfo($"World position: {targetCharacter.transform.position}");

        var components = targetCharacter.GetComponentsInChildren<Component>(true)
            .Where(component => component != null)
            .ToArray();

        _log.LogInfo($"Component count (children included): {components.Length}");

        foreach (var component in components)
        {
            var type = component.GetType();
            var typeName = type.FullName ?? type.Name;
            var isAiCandidate = typeName.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                type == typeof(BaseAI) ||
                                typeof(BaseAI).IsAssignableFrom(type);

            if (!isAiCandidate)
            {
                continue;
            }

            _log.LogInfo($"AI component: {typeName} on GameObject '{component.gameObject.name}'");
            LogMethods(type);
            LogInterestingFields(type, component);
        }

        _log.LogInfo("================= AI DIAGNOSTICS END =================");
    }

    private void LogMethods(Type type)
    {
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .OrderBy(method => method.Name)
            .ToArray();

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            var parameterSignature = string.Join(", ",
                parameters.Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}"));

            _log.LogInfo($"  Method: {method.ReturnType.Name} {method.Name}({parameterSignature})");
        }
    }

    private void LogInterestingFields(Type type, Component instance)
    {
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field =>
                field.FieldType == typeof(Vector3) ||
                field.FieldType == typeof(Transform) ||
                field.FieldType == typeof(GameObject) ||
                field.FieldType == typeof(Character) ||
                field.FieldType == typeof(float) ||
                field.FieldType == typeof(bool))
            .OrderBy(field => field.Name)
            .ToArray();

        foreach (var field in fields)
        {
            object? value;
            try
            {
                value = field.GetValue(instance);
            }
            catch (Exception exception)
            {
                value = $"<error: {exception.GetType().Name}>";
            }

            _log.LogInfo($"  Field: {field.FieldType.Name} {field.Name} = {FormatValue(value)}");
        }
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
        {
            return "null";
        }

        return value switch
        {
            Transform transform => transform.name,
            GameObject gameObject => gameObject.name,
            Character character => character.gameObject.name,
            _ => value.ToString() ?? value.GetType().Name
        };
    }

    private static bool TryGetTargetCharacter(out Character targetCharacter)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var character = hitInfo.collider.GetComponentInParent<Character>();
                if (character != null)
                {
                    targetCharacter = character;
                    return true;
                }
            }
        }

        targetCharacter = null!;
        return false;
    }
}
