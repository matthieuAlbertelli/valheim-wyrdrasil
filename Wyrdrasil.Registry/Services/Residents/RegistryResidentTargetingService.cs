using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Souls.Services;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryResidentTargetingService
{
    private readonly ManualLogSource _log;
    private readonly ResidentRuntimeService _runtimeService;
    private readonly ResidentCatalogService _catalogService;

    public RegistryResidentTargetingService(
        ManualLogSource log,
        ResidentRuntimeService runtimeService,
        ResidentCatalogService catalogService)
    {
        _log = log;
        _runtimeService = runtimeService;
        _catalogService = catalogService;
    }

    public bool TryGetTargetCharacter(out Character targetCharacter)
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

    public bool TryGetTargetRegisteredResident(out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out var targetCharacter))
        {
            resident = null!;
            return false;
        }

        if (!_runtimeService.TryGetResidentId(targetCharacter, out var residentId) || !_catalogService.TryGetResidentById(residentId, out resident))
        {
            resident = null!;
            return false;
        }

        return true;
    }

    public bool TryGetTargetRegisteredResident(string actionLabel, out Character targetCharacter, out RegisteredNpcData resident)
    {
        if (!TryGetTargetCharacter(out targetCharacter))
        {
            _log.LogWarning($"{actionLabel}: no valid character is under the crosshair.");
            resident = null!;
            return false;
        }

        if (!_runtimeService.TryGetResidentId(targetCharacter, out var residentId) || !_catalogService.TryGetResidentById(residentId, out resident))
        {
            _log.LogWarning($"{actionLabel}: the targeted character is not registered.");
            resident = null!;
            return false;
        }

        return true;
    }

    public string DescribeCrosshairTarget()
    {
        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return "camera=null";
        }

        var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        if (!Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            return "raycast=none";
        }

        var hitObject = hitInfo.collider != null ? hitInfo.collider.gameObject : null;
        var hitName = hitObject != null ? hitObject.name : "null";
        var station = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<CraftingStation>() : null;
        var chair = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Chair>() : null;
        var bed = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Bed>() : null;
        var character = hitInfo.collider != null ? hitInfo.collider.GetComponentInParent<Character>() : null;

        return $"hit='{hitName}' point={hitInfo.point} craftingStation={(station != null ? station.gameObject.name : "null")} chair={(chair != null ? chair.gameObject.name : "null")} bed={(bed != null ? bed.gameObject.name : "null")} character={(character != null ? character.gameObject.name : "null")}";
    }
}
