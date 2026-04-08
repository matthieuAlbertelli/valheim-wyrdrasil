using System.Reflection;
using UnityEngine;

namespace Wyrdrasil.Routines.Services;


public sealed class WorldClockService
{
    private static readonly MethodInfo? GetDayFractionMethod = typeof(EnvMan).GetMethod(
        "GetDayFraction",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? SmoothDayFractionField = typeof(EnvMan).GetField(
        "m_smoothDayFraction",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private int? _simulatedMinuteOfDay;

    public bool IsSimulationActive => _simulatedMinuteOfDay.HasValue;

    public bool TryGetCurrentMinuteOfDay(out int minuteOfDay)
    {
        if (_simulatedMinuteOfDay.HasValue)
        {
            minuteOfDay = _simulatedMinuteOfDay.Value;
            return true;
        }

        var envMan = EnvMan.instance;
        if (envMan == null)
        {
            minuteOfDay = 0;
            return false;
        }

        var fraction = TryGetDayFraction(envMan, out var dayFraction)
            ? dayFraction
            : 0f;

        minuteOfDay = Mathf.Clamp(Mathf.RoundToInt(fraction * 24f * 60f), 0, 1439);
        return true;
    }

    public string GetClockLabel()
    {
        return TryGetCurrentMinuteOfDay(out var minuteOfDay)
            ? $"Heure monde : {FormatMinute(minuteOfDay)}"
            : "Heure monde : indisponible";
    }

    public string GetClockModeLabel()
    {
        if (!_simulatedMinuteOfDay.HasValue)
        {
            return "Horloge : temps réel du monde";
        }

        return $"Horloge : simulation active ({FormatMinute(_simulatedMinuteOfDay.Value)})";
    }

    public void SimulateNoon()
    {
        _simulatedMinuteOfDay = 12 * 60;
    }

    public void SimulateNight()
    {
        _simulatedMinuteOfDay = 22 * 60;
    }

    public void ClearSimulation()
    {
        _simulatedMinuteOfDay = null;
    }


    public bool TryGetSimulatedMinuteOfDay(out int minuteOfDay)
    {
        if (_simulatedMinuteOfDay.HasValue)
        {
            minuteOfDay = _simulatedMinuteOfDay.Value;
            return true;
        }

        minuteOfDay = 0;
        return false;
    }

    public void RestoreSimulation(int? simulatedMinuteOfDay)
    {
        _simulatedMinuteOfDay = simulatedMinuteOfDay.HasValue
            ? Mathf.Clamp(simulatedMinuteOfDay.Value, 0, 1439)
            : null;
    }

    private static bool TryGetDayFraction(EnvMan envMan, out float dayFraction)
    {
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

    private static string FormatMinute(int minuteOfDay)
    {
        var normalized = minuteOfDay % 1440;
        if (normalized < 0)
        {
            normalized += 1440;
        }

        var hours = normalized / 60;
        var minutes = normalized % 60;
        return $"{hours:00}:{minutes:00}";
    }
}
