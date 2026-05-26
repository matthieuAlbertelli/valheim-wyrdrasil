using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Settlements.Components;

/// <summary>
/// Shared helper for native Valheim build-piece highlighting.
///
/// This deliberately uses the same low-level lifecycle used by Valheim piece highlights and by
/// established Valheim mods: MaterialMan.SetValue(...), then WearNTear's native ResetHighlight
/// invoke window. It is not a Wyrdrasil mesh shell and it does not mutate renderer.materials directly.
/// </summary>
public static class WyrdrasilNativePieceHighlightState
{
    private const float HighlightResetDelaySeconds = 0.22f;
    private const string ResetHighlightMethodName = "ResetHighlight";

    public static bool ApplyNativeMaterialColor(
        GameObject? target,
        Color color,
        Color emissionColor,
        out bool appliedColor,
        out bool appliedEmission,
        out string backendStatus)
    {
        appliedColor = false;
        appliedEmission = false;

        if (target == null)
        {
            backendStatus = "target unavailable";
            return false;
        }

        appliedColor = NativeMaterialManBridge.TrySetColor(target, "_Color", color);
        appliedColor |= NativeMaterialManBridge.TrySetColor(target, "_BaseColor", color);
        appliedColor |= NativeMaterialManBridge.TrySetColor(target, "_TintColor", color);
        appliedEmission = NativeMaterialManBridge.TrySetColor(target, "_EmissionColor", emissionColor);

        backendStatus = NativeMaterialManBridge.DescribeStatus();
        return appliedColor || appliedEmission;
    }

    public static void RefreshNativeResetWindow(WearNTear? wearNTear)
    {
        if (wearNTear == null)
        {
            return;
        }

        try
        {
            wearNTear.CancelInvoke(ResetHighlightMethodName);
            wearNTear.Invoke(ResetHighlightMethodName, HighlightResetDelaySeconds);
        }
        catch
        {
            // ResetHighlight is private in some Valheim builds. Invoke(string, float) is the native
            // path used by MonoBehaviour; if it is unavailable for any reason, the next vanilla reset
            // will still clear MaterialMan values eventually.
        }
    }

    public static void RequestNativeReset(WearNTear? wearNTear)
    {
        if (wearNTear == null)
        {
            return;
        }

        try
        {
            wearNTear.CancelInvoke(ResetHighlightMethodName);
            wearNTear.Invoke(ResetHighlightMethodName, 0.01f);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    public static string DescribeMaterialBackendStatus()
    {
        return NativeMaterialManBridge.DescribeStatus();
    }

    private static class NativeMaterialManBridge
    {
        private static bool _initialized;
        private static object? _materialManInstance;
        private static MethodInfo? _setColorMethod;
        private static Type? _shaderPropsType;
        private static string? _initializationError;

        public static bool TrySetColor(GameObject target, string shaderPropertyName, Color color)
        {
            EnsureInitialized();
            if (_materialManInstance == null || _setColorMethod == null)
            {
                return false;
            }

            var shaderPropertyId = ResolveShaderPropertyId(shaderPropertyName);
            try
            {
                _setColorMethod.Invoke(_materialManInstance, new object[] { target, shaderPropertyId, color });
                return true;
            }
            catch (Exception exception)
            {
                _initializationError = $"MaterialMan.SetValue<Color> failed for {shaderPropertyName}: {exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        public static string DescribeStatus()
        {
            EnsureInitialized();
            if (_initializationError != null)
            {
                return _initializationError;
            }

            return _materialManInstance != null && _setColorMethod != null
                ? "available"
                : "unavailable";
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var materialManType = FindTypeByName("MaterialMan");
            _shaderPropsType = FindTypeByName("ShaderProps");
            if (materialManType == null)
            {
                _initializationError = "MaterialMan type not found";
                return;
            }

            _materialManInstance = ResolveStaticInstance(materialManType);
            if (_materialManInstance == null)
            {
                _initializationError = "MaterialMan.instance not found";
                return;
            }

            _setColorMethod = materialManType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == "SetValue")
                .Where(method => method.IsGenericMethodDefinition)
                .Select(method => new { Method = method, Parameters = method.GetParameters() })
                .Where(candidate => candidate.Parameters.Length == 3)
                .Where(candidate => typeof(GameObject).IsAssignableFrom(candidate.Parameters[0].ParameterType))
                .Where(candidate => candidate.Parameters[1].ParameterType == typeof(int))
                .Select(candidate => candidate.Method.MakeGenericMethod(typeof(Color)))
                .FirstOrDefault();

            if (_setColorMethod == null)
            {
                _initializationError = "MaterialMan.SetValue<Color>(GameObject,int,Color) not found";
            }
        }

        private static int ResolveShaderPropertyId(string shaderPropertyName)
        {
            if (_shaderPropsType != null)
            {
                var field = _shaderPropsType.GetField(shaderPropertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(int))
                {
                    return (int)field.GetValue(null);
                }
            }

            return Shader.PropertyToID(shaderPropertyName);
        }

        private static object? ResolveStaticInstance(Type type)
        {
            var field = type.GetField("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                return field.GetValue(null);
            }

            var property = type.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return property != null ? property.GetValue(null, null) : null;
        }

        private static Type? FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? directType = null;
                try
                {
                    directType = assembly.GetType(typeName, throwOnError: false);
                }
                catch
                {
                    // Ignore dynamic/reflection-only edge cases.
                }

                if (directType != null)
                {
                    return directType;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(type => type != null).Cast<Type>().ToArray();
                }
                catch
                {
                    continue;
                }

                var match = types.FirstOrDefault(type => type.Name == typeName || type.FullName == typeName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
