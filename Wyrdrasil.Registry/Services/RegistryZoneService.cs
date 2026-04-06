using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Components;
using Wyrdrasil.Registry.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class RegistryZoneService
{
    private const float TavernZoneRadius = 6f;
    private const int RingSegments = 48;

    private readonly ManualLogSource _log;
    private readonly List<FunctionalZoneData> _zones = new();
    private readonly Dictionary<int, WyrdrasilFunctionalZoneMarker> _markers = new();
    private readonly Dictionary<int, GameObject> _zoneRoots = new();

    private int _nextZoneId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<FunctionalZoneData> Zones => _zones;

    public RegistryZoneService(ManualLogSource log, RegistryModeService modeService)
    {
        _log = log;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void CreateTavernZone()
    {
        if (!TryGetPlacementPoint(out var placementPoint))
        {
            _log.LogWarning("Cannot create tavern zone: no valid placement point was found.");
            return;
        }

        var zoneId = _nextZoneId++;
        var zoneData = new FunctionalZoneData(zoneId, ZoneType.Tavern, placementPoint, TavernZoneRadius);
        _zones.Add(zoneData);
        CreateZoneWorldObject(zoneData);
        _log.LogInfo($"Created Tavern zone #{zoneData.Id} at {zoneData.Position}.");
    }

    public bool TryGetPlacementPoint(out Vector3 placementPoint)
    {
        var localPlayer = Player.m_localPlayer;
        if (!localPlayer)
        {
            placementPoint = Vector3.zero;
            return false;
        }

        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            if (Physics.Raycast(ray, out var hitInfo, 100f, ~0, QueryTriggerInteraction.Ignore))
            {
                placementPoint = hitInfo.point;
                placementPoint.y += 0.05f;
                return true;
            }
        }

        placementPoint = localPlayer.transform.position + localPlayer.transform.forward * 5f;
        var zoneSystem = ZoneSystem.instance;
        if (zoneSystem != null)
        {
            placementPoint.y = zoneSystem.GetGroundHeight(placementPoint) + 0.05f;
        }

        return true;
    }

    public bool TryFindZoneAtPoint(Vector3 point, out FunctionalZoneData zone)
    {
        var match = FindZoneContainingPoint(point, ZoneType.Tavern);
        if (match != null)
        {
            zone = match;
            return true;
        }

        zone = null!;
        return false;
    }

    public bool DeleteZone(int zoneId)
    {
        var removed = _zones.RemoveAll(zone => zone.Id == zoneId) > 0;
        if (!removed)
        {
            return false;
        }

        if (_zoneRoots.TryGetValue(zoneId, out var root) && root != null)
        {
            Object.Destroy(root);
        }

        _zoneRoots.Remove(zoneId);
        _markers.Remove(zoneId);
        return true;
    }

    public FunctionalZoneData? FindZoneContainingPoint(Vector3 point, ZoneType zoneType)
    {
        FunctionalZoneData? bestMatch = null;
        var bestDistance = float.MaxValue;

        foreach (var zone in _zones)
        {
            if (zone.ZoneType != zoneType)
            {
                continue;
            }

            var zoneCenter2D = new Vector2(zone.Position.x, zone.Position.z);
            var point2D = new Vector2(point.x, point.z);
            var distance = Vector2.Distance(zoneCenter2D, point2D);
            if (distance > zone.Radius || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestMatch = zone;
        }

        return bestMatch;
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values)
        {
            marker.SetVisualizationVisible(isEnabled);
        }
    }

    private void CreateZoneWorldObject(FunctionalZoneData zoneData)
    {
        var root = new GameObject($"Wyrdrasil_FunctionalZone_{zoneData.ZoneType}_{zoneData.Id}");
        root.transform.position = zoneData.Position;
        _zoneRoots[zoneData.Id] = root;

        var marker = root.AddComponent<WyrdrasilFunctionalZoneMarker>();
        marker.Initialize(zoneData.Id, zoneData.ZoneType, zoneData.Radius);

        var ringObject = new GameObject("ZoneRing");
        ringObject.transform.SetParent(root.transform, false);
        ringObject.transform.localPosition = Vector3.zero;

        var lineRenderer = ringObject.AddComponent<LineRenderer>();
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = false;
        lineRenderer.positionCount = RingSegments;
        lineRenderer.startWidth = 0.08f;
        lineRenderer.endWidth = 0.08f;
        lineRenderer.material = CreateLineMaterial();
        lineRenderer.startColor = new Color(0.95f, 0.7f, 0.2f, 1f);
        lineRenderer.endColor = new Color(0.95f, 0.7f, 0.2f, 1f);
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        for (var i = 0; i < RingSegments; i++)
        {
            var angle = i / (float)RingSegments * Mathf.PI * 2f;
            var x = Mathf.Cos(angle) * zoneData.Radius;
            var z = Mathf.Sin(angle) * zoneData.Radius;
            lineRenderer.SetPosition(i, new Vector3(x, 0.03f, z));
        }

        marker.RegisterRenderer(lineRenderer);
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[zoneData.Id] = marker;
    }

    private static Material CreateLineMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }
}
