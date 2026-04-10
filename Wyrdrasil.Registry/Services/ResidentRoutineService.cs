using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Routines.Services;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class ResidentRoutineService
{
    private const float EvaluationIntervalSeconds = 0.5f;

    private readonly ManualLogSource _log;
    private readonly WorldClockService _worldClockService;
    private readonly RegistryResidentService _residentService;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly ResidentOccupationService _occupationService;
    private readonly Dictionary<int, ResidentRoutineActivityType> _appliedActivitiesByResidentId = new();

    private float _nextEvaluationTime;
    private bool _hasScheduledForcedRefresh;
    private float _scheduledForcedRefreshTime;
    private bool _scheduledForcedRefreshClearsAppliedActivities;

    public ResidentRoutineService(
        ManualLogSource log,
        WorldClockService worldClockService,
        RegistryResidentService residentService,
        ResidentRuntimeService runtimeService,
        ResidentOccupationService occupationService)
    {
        _log = log;
        _worldClockService = worldClockService;
        _residentService = residentService;
        _runtimeService = runtimeService;
        _occupationService = occupationService;
    }

    public void Update()
    {
        if (_hasScheduledForcedRefresh && Time.time >= _scheduledForcedRefreshTime)
        {
            ForceRefreshAllResidents(_scheduledForcedRefreshClearsAppliedActivities);
            _hasScheduledForcedRefresh = false;
        }

        if (Time.time < _nextEvaluationTime)
        {
            return;
        }

        _nextEvaluationTime = Time.time + EvaluationIntervalSeconds;

        if (!_worldClockService.TryGetCurrentMinuteOfDay(out var minuteOfDay))
        {
            return;
        }

        foreach (var resident in _residentService.RegisteredNpcs)
        {
            EvaluateResident(resident, minuteOfDay);
        }
    }

    public void ScheduleForcedRefresh(float delaySeconds, bool clearAppliedActivities = false)
    {
        _scheduledForcedRefreshTime = Time.time + Mathf.Max(0f, delaySeconds);
        _scheduledForcedRefreshClearsAppliedActivities = clearAppliedActivities;
        _hasScheduledForcedRefresh = true;
    }

    public void ForceRefreshAllResidents(bool clearAppliedActivities = false)
    {
        if (clearAppliedActivities)
        {
            _appliedActivitiesByResidentId.Clear();
        }

        if (!_worldClockService.TryGetCurrentMinuteOfDay(out var minuteOfDay))
        {
            return;
        }

        foreach (var resident in _residentService.RegisteredNpcs)
        {
            EvaluateResident(resident, minuteOfDay);
        }

        _nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
    }

    private void EvaluateResident(RegisteredNpcData resident, int minuteOfDay)
    {
        if (_runtimeService.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
        {
            return;
        }

        var desiredActivity = DetermineDesiredActivity(resident, minuteOfDay);
        var currentActivity = _appliedActivitiesByResidentId.TryGetValue(resident.Id, out var appliedActivity)
            ? appliedActivity
            : ResidentRoutineActivityType.None;

        if (desiredActivity == currentActivity)
        {
            ContinueActivity(resident, desiredActivity);
            return;
        }

        if (currentActivity != ResidentRoutineActivityType.None)
        {
            _occupationService.ReleaseOccupation(resident, true);
        }

        if (desiredActivity == ResidentRoutineActivityType.None)
        {
            _appliedActivitiesByResidentId.Remove(resident.Id);
            return;
        }

        var applied = _occupationService.TryStartOccupation(resident, desiredActivity);
        if (applied)
        {
            _appliedActivitiesByResidentId[resident.Id] = desiredActivity;
            _log.LogInfo($"Resident #{resident.Id} routine activity -> {desiredActivity}.");
            return;
        }

        _appliedActivitiesByResidentId.Remove(resident.Id);
    }

    private static ResidentRoutineActivityType DetermineDesiredActivity(RegisteredNpcData resident, int minuteOfDay)
    {
        var bestEntry = resident.ScheduleEntries
            .Where(entry => entry.ContainsMinute(minuteOfDay))
            .OrderByDescending(entry => entry.Priority)
            .ThenBy(entry => (int)entry.ActivityType)
            .FirstOrDefault();

        return bestEntry?.ActivityType ?? ResidentRoutineActivityType.None;
    }

    private void ContinueActivity(RegisteredNpcData resident, ResidentRoutineActivityType activityType)
    {
        _occupationService.ContinueOccupation(resident, activityType);
    }
}
