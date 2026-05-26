using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Settlements.Services;

namespace Wyrdrasil.Registry.Services;

/// <summary>
/// Keeps craft-station assignments aligned with the actual Valheim world.
///
/// A workbench can be destroyed through the vanilla hammer instead of through
/// Wyrdrasil's delete action. In that case Valheim removes the runtime object,
/// but Wyrdrasil still has a persisted designation and residents may still point
/// to it. This service performs the cross-module cleanup at the Registry layer,
/// where both settlements and residents are available.
/// </summary>
public sealed class RegistryCraftStationIntegrityService
{
    private const float ScanIntervalSeconds = 1.25f;
    private const float LocalPlayerPruneRadius = 64f;

    private readonly ManualLogSource _log;
    private readonly CraftStationService _craftStationService;
    private readonly SettlementsPersistenceParticipant _settlementsPersistenceParticipant;
    private readonly RegistryResidentService _residentService;
    private readonly RegistryPersistenceService _persistenceService;
    private float _nextScanTime;

    public RegistryCraftStationIntegrityService(
        ManualLogSource log,
        CraftStationService craftStationService,
        SettlementsPersistenceParticipant settlementsPersistenceParticipant,
        RegistryResidentService residentService,
        RegistryPersistenceService persistenceService)
    {
        _log = log;
        _craftStationService = craftStationService;
        _settlementsPersistenceParticipant = settlementsPersistenceParticipant;
        _residentService = residentService;
        _persistenceService = persistenceService;
    }

    public void Update()
    {
        if (Time.unscaledTime < _nextScanTime)
        {
            return;
        }

        _nextScanTime = Time.unscaledTime + ScanIntervalSeconds;

        var localPlayer = Player.m_localPlayer;
        if (localPlayer == null)
        {
            return;
        }

        var observerPosition = localPlayer.transform.position;
        var deletedCraftStationIds = new HashSet<int>();

        foreach (var craftStationId in _craftStationService.PruneMissingRuntimeCraftStationsNear(observerPosition, LocalPlayerPruneRadius))
        {
            deletedCraftStationIds.Add(craftStationId);
        }

        foreach (var craftStationId in _settlementsPersistenceParticipant.PruneUnresolvedCraftStationsNear(observerPosition, LocalPlayerPruneRadius))
        {
            deletedCraftStationIds.Add(craftStationId);
        }

        if (deletedCraftStationIds.Count == 0)
        {
            return;
        }

        foreach (var craftStationId in deletedCraftStationIds)
        {
            _residentService.HandleDeletedCraftStation(craftStationId);
        }

        _persistenceService.SaveWorldState();
        _log.LogInfo($"[CraftStation][Integrity] Cleaned stale craft station designation(s): {string.Join(", ", deletedCraftStationIds.OrderBy(id => id))}.");
    }
}
