using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionGameTimeService
{
    private static readonly MethodInfo? GetDayFractionMethod = typeof(EnvMan).GetMethod(
        "GetDayFraction",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? SmoothDayFractionField = typeof(EnvMan).GetField(
        "m_smoothDayFraction",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private bool _hasPreviousSample;
    private float _previousDayFraction;

    public void Reset()
    {
        _hasPreviousSample = false;
        _previousDayFraction = 0f;
    }

    public float ConsumeDeltaGameHours()
    {
        if (!TryGetCurrentDayFraction(out var currentDayFraction))
        {
            _hasPreviousSample = false;
            _previousDayFraction = 0f;
            return 0f;
        }

        if (!_hasPreviousSample)
        {
            _hasPreviousSample = true;
            _previousDayFraction = currentDayFraction;
            return 0f;
        }

        var deltaFraction = currentDayFraction - _previousDayFraction;
        if (deltaFraction < 0f)
        {
            deltaFraction += 1f;
        }

        _previousDayFraction = currentDayFraction;
        return Mathf.Max(0f, deltaFraction) * 24f;
    }

    private static bool TryGetCurrentDayFraction(out float dayFraction)
    {
        var envMan = EnvMan.instance;
        if (envMan == null)
        {
            dayFraction = 0f;
            return false;
        }

        if (GetDayFractionMethod != null)
        {
            try
            {
                var result = GetDayFractionMethod.Invoke(envMan, null);
                if (result is float typedResult)
                {
                    dayFraction = Mathf.Repeat(typedResult, 1f);
                    return true;
                }
            }
            catch
            {
                // Fall through to field lookup.
            }
        }

        if (SmoothDayFractionField != null)
        {
            try
            {
                var result = SmoothDayFractionField.GetValue(envMan);
                if (result is float typedResult)
                {
                    dayFraction = Mathf.Repeat(typedResult, 1f);
                    return true;
                }
            }
            catch
            {
                // Ignore and fail.
            }
        }

        dayFraction = 0f;
        return false;
    }
}
