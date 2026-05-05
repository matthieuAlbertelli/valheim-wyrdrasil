using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Routines.Runtime;
using Wyrdrasil.Souls.Runtime;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class ResidentRoutineService
{
    private const float EvaluationIntervalSeconds = 0.5f;

    private readonly ManualLogSource _log;
    private readonly IRoutinesRuntimeApi _routinesRuntimeApi;
    private readonly ISoulsRuntimeApi _soulsRuntimeApi;
    private readonly Dictionary<int, ResidentRoutineActivityType> _appliedActivitiesByResidentId = new();

    private float _nextEvaluationTime;
    private bool _hasScheduledForcedRefresh;
    private float _scheduledForcedRefreshTime;
    private bool _scheduledForcedRefreshClearsAppliedActivities;

    public ResidentRoutineService(
        ManualLogSource log,
        IRoutinesRuntimeApi routinesRuntimeApi,
        ISoulsRuntimeApi soulsRuntimeApi)
    {
        _log = log;
        _routinesRuntimeApi = routinesRuntimeApi;
        _soulsRuntimeApi = soulsRuntimeApi;
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

        if (!_routinesRuntimeApi.TryGetCurrentMinuteOfDay(out var minuteOfDay))
        {
            return;
        }

        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
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
            ReleaseAppliedActivities();
        }

        if (!_routinesRuntimeApi.TryGetCurrentMinuteOfDay(out var minuteOfDay))
        {
            return;
        }

        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
        {
            EvaluateResident(resident, minuteOfDay);
        }

        _nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
    }

    public void ForceRefreshResident(RegisteredNpcData resident, bool clearAppliedActivity = false)
    {
        if (clearAppliedActivity)
        {
            ReleaseAppliedActivity(resident);
        }

        if (!_routinesRuntimeApi.TryGetCurrentMinuteOfDay(out var minuteOfDay))
        {
            return;
        }

        EvaluateResident(resident, minuteOfDay);
        _nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
    }

    public void ClearResidentActivityState(RegisteredNpcData resident, bool releaseOccupation = true)
    {
        if (releaseOccupation)
        {
            ReleaseAppliedActivity(resident);
            return;
        }

        _appliedActivitiesByResidentId.Remove(resident.Id);
    }

    private void EvaluateResident(RegisteredNpcData resident, int minuteOfDay)
    {
        if (_soulsRuntimeApi.GetRuntimeState(resident.Id) == ResidentRuntimeState.Spawning)
        {
            return;
        }

        var desiredActivity = DetermineDesiredActivity(resident, minuteOfDay);
        var currentActivity = _appliedActivitiesByResidentId.TryGetValue(resident.Id, out var appliedActivity)
            ? appliedActivity
            : ResidentRoutineActivityType.None;

        if (desiredActivity == currentActivity)
        {
            if (desiredActivity != ResidentRoutineActivityType.None)
            {
                ContinueActivity(resident, desiredActivity);
            }

            return;
        }

        if (currentActivity != ResidentRoutineActivityType.None)
        {
            _routinesRuntimeApi.ReleaseOccupation(resident, true);
        }

        if (desiredActivity == ResidentRoutineActivityType.None)
        {
            _appliedActivitiesByResidentId.Remove(resident.Id);
            return;
        }

        var applied = _routinesRuntimeApi.TryStartOccupation(resident, desiredActivity);
        if (applied)
        {
            _appliedActivitiesByResidentId[resident.Id] = desiredActivity;
            _log.LogInfo($"Resident #{resident.Id} routine activity -> {desiredActivity}.");
            return;
        }

        _appliedActivitiesByResidentId.Remove(resident.Id);
    }

    private void ReleaseAppliedActivities()
    {
        foreach (var residentId in _appliedActivitiesByResidentId.Keys.ToArray())
        {
            if (_soulsRuntimeApi.TryGetResidentById(residentId, out var resident))
            {
                _routinesRuntimeApi.ReleaseOccupation(resident, true);
            }
        }

        _appliedActivitiesByResidentId.Clear();
    }

    private void ReleaseAppliedActivity(RegisteredNpcData resident)
    {
        if (!_appliedActivitiesByResidentId.Remove(resident.Id))
        {
            return;
        }

        _routinesRuntimeApi.ReleaseOccupation(resident, true);
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
        _routinesRuntimeApi.ContinueOccupation(resident, activityType);
    }
}
