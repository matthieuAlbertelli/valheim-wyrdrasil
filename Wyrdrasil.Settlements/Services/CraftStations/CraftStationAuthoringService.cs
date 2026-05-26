using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Settlements.Services.CraftStations;

public sealed class CraftStationAuthoringService
{
    private const float ResolveStationDistance = 2.5f;

    private readonly ManualLogSource _log;
    private readonly BuildingService _buildingService;
    private readonly FunctionalZoneService _zoneService;
    private readonly ZonePlacementPolicyService _anchorPolicyService;
    private readonly CraftStationRegistryService _registryService;

    public CraftStationAuthoringService(
        ManualLogSource log,
        BuildingService buildingService,
        FunctionalZoneService zoneService,
        ZonePlacementPolicyService anchorPolicyService,
        CraftStationRegistryService registryService)
    {
        _log = log;
        _buildingService = buildingService;
        _zoneService = zoneService;
        _anchorPolicyService = anchorPolicyService;
        _registryService = registryService;
    }

    public bool TryResolveCraftStationFromSave(RegisteredCraftStationSaveData saveData, out RegisteredCraftStationData craftStationData)
    {
        var referencePosition = saveData.ReferenceWorldPosition.ToVector3();
        var allStations = Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None);

        var exactCandidates = allStations
            .Select(station => new
            {
                Station = station,
                PersistentId = BuildPersistentFurnitureId(station),
                Distance = Vector3.Distance(GetReferencePosition(station), referencePosition)
            })
            .Where(candidate => candidate.PersistentId == saveData.PersistentFurnitureId)
            .OrderBy(candidate => candidate.Distance)
            .ToList();

        if (exactCandidates.Count > 0)
        {
            var exactMatch = exactCandidates[0];
            if (exactMatch.Distance <= ResolveStationDistance)
            {
                craftStationData = BuildResolvedCraftStation(saveData, exactMatch.Station, exactMatch.PersistentId);
                _log.LogInfo($"[CraftStation][Resolve] Restored station #{craftStationData.Id} with exact persistent match '{exactMatch.PersistentId}'.");
                return true;
            }
        }

        var fallbackStation = allStations
            .OrderBy(station => Vector3.Distance(GetReferencePosition(station), referencePosition))
            .FirstOrDefault();

        if (fallbackStation != null)
        {
            var fallbackDistance = Vector3.Distance(GetReferencePosition(fallbackStation), referencePosition);
            if (fallbackDistance <= ResolveStationDistance)
            {
                craftStationData = BuildResolvedCraftStation(saveData, fallbackStation, BuildPersistentFurnitureId(fallbackStation));
                _log.LogWarning($"[CraftStation][Resolve] Restored station #{craftStationData.Id} with fallback nearest-station resolution. distance={fallbackDistance:0.00}");
                return true;
            }
        }

        craftStationData = null!;
        _log.LogWarning($"[CraftStation][Resolve] Unable to resolve craft station #{saveData.Id} ('{saveData.DisplayName}') near {referencePosition}.");
        return false;
    }

    public void DesignateCraftStationAtCrosshair()
    {
        if (!TryGetOrDesignateCraftStationAtCrosshair(out _, out var failureReason))
        {
            _log.LogWarning($"[CraftStation][Authoring] {failureReason}");
        }
    }

    public bool TryGetOrDesignateCraftStationAtCrosshair(out RegisteredCraftStationData craftStationData, out string failureReason)
    {
        if (!TryGetRuntimeCraftStationAtCrosshair(out var furnitureRoot, out var craftingStation))
        {
            craftStationData = null!;
            failureReason = "Cannot designate craft station: targeted object is not a valid Valheim crafting station.";
            return false;
        }

        var existingStation = _registryService.FindCraftStationByFurniture(furnitureRoot);
        if (existingStation != null)
        {
            craftStationData = existingStation;
            failureReason = string.Empty;
            return true;
        }

        var zone = _zoneService.FindZoneContainingPointHorizontally(GetReferencePosition(craftingStation));
        var shouldAssociateZone = zone != null && _anchorPolicyService.ShouldAssociateCraftStationWithZone(zone.ZoneType);
        if (zone == null && !_anchorPolicyService.CanDesignateCraftStationStandalone())
        {
            craftStationData = null!;
            failureReason = "Cannot designate craft station: this station requires a functional zone, but no zone was found.";
            return false;
        }

        var persistentFurnitureId = BuildPersistentFurnitureId(craftingStation);
        var referenceWorldPosition = GetReferencePosition(craftingStation);
        var profile = ResolveProfileForFurniture(furnitureRoot.name);

        var buildingId = zone != null
            ? zone.BuildingId
            : _buildingService.CreateImplicitBuildingForDesignation("Craft Station", referenceWorldPosition).Id;

        var zoneId = shouldAssociateZone && zone != null
            ? zone.Id
            : (int?)null;

        var data = _registryService.AddCraftStation(
            buildingId,
            zoneId,
            furnitureRoot.name,
            persistentFurnitureId,
            referenceWorldPosition,
            profile.DefaultLocalAnchorPosition,
            profile.DefaultLocalAnchorForward,
            profile.ProfileId,
            furnitureRoot,
            craftingStation);

        craftStationData = data;
        failureReason = string.Empty;
        if (zoneId.HasValue)
        {
            _log.LogInfo($"[CraftStation][Authoring] Designated station #{data.Id} on '{data.DisplayName}' in zone #{zoneId.Value} (building #{buildingId}) with profile='{profile.ProfileId}' and persistentId='{persistentFurnitureId}'.");
        }
        else
        {
            _log.LogInfo($"[CraftStation][Authoring] Designated standalone station #{data.Id} on '{data.DisplayName}' in building #{buildingId} with profile='{profile.ProfileId}' and persistentId='{persistentFurnitureId}'.");
        }

        return true;
    }

    public bool TryGetCraftStationAtCrosshair(out RegisteredCraftStationData craftStationData)
    {
        if (!TryGetRuntimeCraftStationAtCrosshair(out var furnitureRoot, out _))
        {
            craftStationData = null!;
            return false;
        }

        var existingStation = _registryService.FindCraftStationByFurniture(furnitureRoot);
        if (existingStation == null)
        {
            craftStationData = null!;
            return false;
        }

        craftStationData = existingStation;
        return true;
    }

    public bool TryGetInteractionProfile(RegisteredCraftStationData station, out CraftStationInteractionProfile profile)
    {
        if (CraftStationInteractionProfileRegistry.TryGetProfileById(station.InteractionProfileId, out profile))
        {
            return true;
        }

        profile = CraftStationInteractionProfileRegistry.GetDefaultProfile();
        return true;
    }

    public bool TryGetCraftStationIdAtCrosshair(out int craftStationId)
    {
        craftStationId = 0;
        if (!TryGetRuntimeCraftStationAtCrosshair(out var furnitureRoot, out _))
        {
            return false;
        }

        var existingStation = _registryService.FindCraftStationByFurniture(furnitureRoot);
        if (existingStation == null)
        {
            return false;
        }

        craftStationId = existingStation.Id;
        return true;
    }

    private RegisteredCraftStationData BuildResolvedCraftStation(RegisteredCraftStationSaveData saveData, CraftingStation runtimeStation, string persistentId)
    {
        var furnitureRoot = CraftStationRuntimeBindingResolver.ResolveFurnitureRoot(runtimeStation);
        var profile = ResolveProfileForRestoredStation(saveData, furnitureRoot.name);
        var craftStationData = new RegisteredCraftStationData(
            saveData.Id,
            saveData.BuildingId,
            saveData.ZoneId,
            saveData.DisplayName,
            persistentId,
            saveData.ReferenceWorldPosition.ToVector3(),
            saveData.AnchorLocalPosition.ToVector3(),
            saveData.AnchorLocalForward.ToVector3(),
            profile.ProfileId);

        craftStationData.UpdateRuntimeBinding(furnitureRoot, runtimeStation, GetReferencePosition(runtimeStation));
        if (saveData.AssignedRegisteredNpcId.HasValue)
        {
            craftStationData.AssignRegisteredNpc(saveData.AssignedRegisteredNpcId.Value);
        }

        return craftStationData;
    }

    private static CraftStationInteractionProfile ResolveProfileForFurniture(string prefabName)
    {
        if (CraftStationInteractionProfileRegistry.TryGetProfileForPrefab(prefabName, out var profile))
        {
            return profile;
        }

        return CraftStationInteractionProfileRegistry.GetDefaultProfile();
    }

    private static CraftStationInteractionProfile ResolveProfileForRestoredStation(RegisteredCraftStationSaveData saveData, string prefabName)
    {
        if (CraftStationInteractionProfileRegistry.TryGetProfileById(saveData.InteractionProfileId, out var savedProfile))
        {
            return savedProfile;
        }

        return ResolveProfileForFurniture(prefabName);
    }

    private static bool TryGetRuntimeCraftStationAtCrosshair(out GameObject furnitureRoot, out CraftingStation craftingStation)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                var station = hitInfo.collider.GetComponentInParent<CraftingStation>();
                if (station != null)
                {
                    furnitureRoot = CraftStationRuntimeBindingResolver.ResolveFurnitureRoot(station);
                    craftingStation = station;
                    return true;
                }
            }
        }

        furnitureRoot = null!;
        craftingStation = null!;
        return false;
    }

    private static Vector3 GetReferencePosition(CraftingStation craftingStation)
    {
        return craftingStation.transform.position;
    }

    private static string BuildPersistentFurnitureId(CraftingStation craftingStation)
    {
        var nview = craftingStation.GetComponentInParent<ZNetView>();
        var referenceTransform = craftingStation.transform;

        if (nview != null && nview.GetZDO() != null)
        {
            var localPosition = nview.transform.InverseTransformPoint(referenceTransform.position);
            return $"zdo:{nview.GetZDO().m_uid}:craft:{craftingStation.gameObject.name}:{localPosition.x:0.000}:{localPosition.y:0.000}:{localPosition.z:0.000}";
        }

        var worldPosition = referenceTransform.position;
        return $"fallback:{craftingStation.gameObject.name}:{worldPosition.x:0.000}:{worldPosition.y:0.000}:{worldPosition.z:0.000}";
    }
}
