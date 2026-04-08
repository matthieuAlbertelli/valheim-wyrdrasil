using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Registry.Tool;
using Wyrdrasil.Core.Services;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Settlements.Components;

namespace Wyrdrasil.Settlements.Services;


public sealed class RegistryWaypointService
{
    private const float DuplicateWaypointDistance = 0.25f;
    private const float StartWaypointSnapDistance = 1.50f;

    private readonly ManualLogSource _log;
    private readonly RegistryZoneService _zoneService;
    private readonly List<NavigationWaypointData> _waypoints = new();
    private readonly Dictionary<int, WyrdrasilNavigationWaypointMarker> _markers = new();
    private readonly Dictionary<int, HashSet<int>> _links = new();
    private readonly Dictionary<string, LineRenderer> _linkRenderers = new();
    private readonly Dictionary<int, GameObject> _waypointRoots = new();

    private int _nextWaypointId = 1;
    private bool _visualsVisible;

    public IReadOnlyList<NavigationWaypointData> Waypoints => _waypoints;
    public int NextWaypointId => _nextWaypointId;
    public int? PendingLinkStartWaypointId { get; private set; }

    public RegistryWaypointService(ManualLogSource log, RegistryModeService modeService, RegistryZoneService zoneService)
    {
        _log = log;
        _zoneService = zoneService;
        _visualsVisible = modeService.IsRegistryModeEnabled;
        modeService.RegistryModeChanged += OnRegistryModeChanged;
    }

    public void ClearAllWaypoints()
    {
        foreach (var root in _waypointRoots.Values)
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        foreach (var lineRenderer in _linkRenderers.Values)
        {
            if (lineRenderer != null)
            {
                Object.Destroy(lineRenderer.gameObject);
            }
        }

        _waypoints.Clear();
        _markers.Clear();
        _links.Clear();
        _linkRenderers.Clear();
        _waypointRoots.Clear();
        PendingLinkStartWaypointId = null;
        _nextWaypointId = 1;
    }

    public void LoadWaypoints(IEnumerable<NavigationWaypointData> waypoints, IEnumerable<NavigationWaypointLinkSaveData> links, int nextWaypointId)
    {
        ClearAllWaypoints();

        foreach (var waypoint in waypoints.OrderBy(candidate => candidate.Id))
        {
            _waypoints.Add(waypoint);
            _links[waypoint.Id] = new HashSet<int>();
            CreateWaypointWorldObject(waypoint);
        }

        foreach (var link in links)
        {
            var waypointAId = Mathf.Min(link.WaypointAId, link.WaypointBId);
            var waypointBId = Mathf.Max(link.WaypointAId, link.WaypointBId);

            if (waypointAId == waypointBId)
            {
                _log.LogWarning($"Skipping persisted waypoint link #{waypointAId} <-> #{waypointBId}: a waypoint cannot be linked to itself.");
                continue;
            }

            if (!_links.ContainsKey(waypointAId) || !_links.ContainsKey(waypointBId))
            {
                _log.LogWarning($"Skipping persisted waypoint link #{waypointAId} <-> #{waypointBId}: one of the waypoints is missing.");
                continue;
            }

            CreateBidirectionalLink(waypointAId, waypointBId, createVisual: true, shouldLog: false);
        }

        _nextWaypointId = nextWaypointId;
        PendingLinkStartWaypointId = null;
        UpdateWaypointSelectionVisuals();
    }

    public IReadOnlyList<NavigationWaypointLinkSaveData> GetPersistedLinks()
    {
        var links = new List<NavigationWaypointLinkSaveData>();
        var seen = new HashSet<string>();

        foreach (var pair in _links)
        {
            foreach (var neighborId in pair.Value)
            {
                var waypointAId = Mathf.Min(pair.Key, neighborId);
                var waypointBId = Mathf.Max(pair.Key, neighborId);
                var linkKey = GetLinkKey(waypointAId, waypointBId);

                if (!seen.Add(linkKey))
                {
                    continue;
                }

                links.Add(new NavigationWaypointLinkSaveData
                {
                    WaypointAId = waypointAId,
                    WaypointBId = waypointBId
                });
            }
        }

        return links;
    }

    public void CreateNavigationWaypoint()
    {
        if (!_zoneService.TryGetPlacementPoint(out var placementPoint))
        {
            _log.LogWarning("Cannot create waypoint: no valid placement point was found.");
            return;
        }

        var waypointData = new NavigationWaypointData(_nextWaypointId++, placementPoint);
        _waypoints.Add(waypointData);
        _links[waypointData.Id] = new HashSet<int>();
        CreateWaypointWorldObject(waypointData);
        _log.LogInfo($"Created navigation waypoint #{waypointData.Id} at {waypointData.Position}.");
    }

    public bool DeleteWaypointAtCrosshair()
    {
        if (!TryGetTargetWaypoint(out var targetWaypoint))
        {
            return false;
        }

        DeleteWaypoint(targetWaypoint.Id);
        _log.LogInfo($"Deleted navigation waypoint #{targetWaypoint.Id}.");
        return true;
    }

    public void ConnectNavigationWaypoints()
    {
        if (!TryGetTargetWaypoint(out var targetWaypoint))
        {
            _log.LogWarning("Cannot connect waypoints: no navigation waypoint is under the crosshair.");
            return;
        }

        if (!PendingLinkStartWaypointId.HasValue)
        {
            PendingLinkStartWaypointId = targetWaypoint.Id;
            UpdateWaypointSelectionVisuals();
            _log.LogInfo($"Waypoint link start selected: #{targetWaypoint.Id}.");
            return;
        }

        var startId = PendingLinkStartWaypointId.Value;
        if (startId == targetWaypoint.Id)
        {
            PendingLinkStartWaypointId = null;
            UpdateWaypointSelectionVisuals();
            _log.LogInfo("Waypoint link selection cleared.");
            return;
        }

        CreateBidirectionalLink(startId, targetWaypoint.Id, createVisual: true, shouldLog: true);
        PendingLinkStartWaypointId = null;
        UpdateWaypointSelectionVisuals();
    }

    public bool TryBuildRoute(Vector3 startPosition, Vector3 endPosition, out List<Vector3> routePoints)
    {
        routePoints = new List<Vector3>();
        if (_waypoints.Count == 0)
        {
            return false;
        }

        var startWaypoint = FindNearestWaypoint(startPosition);
        var endWaypoint = FindNearestWaypoint(endPosition);
        if (startWaypoint == null || endWaypoint == null || startWaypoint.Id == endWaypoint.Id)
        {
            return false;
        }

        if (!TryFindShortestPath(startWaypoint.Id, endWaypoint.Id, out var waypointPath) || waypointPath.Count == 0)
        {
            return false;
        }

        var startIndex = IsCloseToWaypoint(startPosition, startWaypoint.Position) ? 1 : 0;
        for (var i = startIndex; i < waypointPath.Count; i++)
        {
            var waypointId = waypointPath[i];
            var waypoint = _waypoints.FirstOrDefault(candidate => candidate.Id == waypointId);
            if (waypoint != null)
            {
                AddWaypointPositionIfMeaningful(routePoints, waypoint.Position);
            }
        }

        return routePoints.Count > 0;
    }

    private static bool IsCloseToWaypoint(Vector3 worldPosition, Vector3 waypointPosition)
    {
        var horizontalDelta = waypointPosition - worldPosition;
        horizontalDelta.y = 0f;
        return horizontalDelta.magnitude <= StartWaypointSnapDistance;
    }

    private static void AddWaypointPositionIfMeaningful(List<Vector3> routePoints, Vector3 candidatePosition)
    {
        if (routePoints.Count == 0)
        {
            routePoints.Add(candidatePosition);
            return;
        }

        if (Vector3.Distance(routePoints[routePoints.Count - 1], candidatePosition) > DuplicateWaypointDistance)
        {
            routePoints.Add(candidatePosition);
        }
    }

    private void DeleteWaypoint(int waypointId)
    {
        _waypoints.RemoveAll(waypoint => waypoint.Id == waypointId);
        if (_waypointRoots.TryGetValue(waypointId, out var root) && root != null)
        {
            Object.Destroy(root);
        }

        _waypointRoots.Remove(waypointId);
        _markers.Remove(waypointId);

        if (_links.TryGetValue(waypointId, out var neighbors))
        {
            foreach (var neighborId in neighbors.ToArray())
            {
                if (_links.TryGetValue(neighborId, out var neighborLinks))
                {
                    neighborLinks.Remove(waypointId);
                }

                var linkKey = GetLinkKey(waypointId, neighborId);
                if (_linkRenderers.TryGetValue(linkKey, out var lineRenderer) && lineRenderer != null)
                {
                    Object.Destroy(lineRenderer.gameObject);
                }

                _linkRenderers.Remove(linkKey);
            }
        }

        _links.Remove(waypointId);
        if (PendingLinkStartWaypointId == waypointId)
        {
            PendingLinkStartWaypointId = null;
        }

        UpdateWaypointSelectionVisuals();
    }

    private void CreateBidirectionalLink(int waypointAId, int waypointBId, bool createVisual, bool shouldLog)
    {
        if (!_links.TryGetValue(waypointAId, out var linksA) || !_links.TryGetValue(waypointBId, out var linksB))
        {
            if (shouldLog)
            {
                _log.LogWarning("Cannot connect waypoints: one of the waypoint ids is unknown.");
            }
            return;
        }

        if (linksA.Contains(waypointBId))
        {
            if (shouldLog)
            {
                _log.LogInfo($"Waypoint link already exists between #{waypointAId} and #{waypointBId}.");
            }
            return;
        }

        linksA.Add(waypointBId);
        linksB.Add(waypointAId);

        if (createVisual)
        {
            CreateLinkVisual(waypointAId, waypointBId);
        }

        if (shouldLog)
        {
            _log.LogInfo($"Connected waypoint #{waypointAId} <-> #{waypointBId}.");
        }
    }

    private bool TryFindShortestPath(int startWaypointId, int endWaypointId, out List<int> path)
    {
        path = new List<int>();
        var unvisited = new HashSet<int>(_waypoints.Select(waypoint => waypoint.Id));
        var distances = new Dictionary<int, float>();
        var previous = new Dictionary<int, int?>();

        foreach (var waypointId in unvisited)
        {
            distances[waypointId] = float.MaxValue;
            previous[waypointId] = null;
        }

        distances[startWaypointId] = 0f;

        while (unvisited.Count > 0)
        {
            var currentId = unvisited.OrderBy(waypointId => distances[waypointId]).First();
            if (distances[currentId] == float.MaxValue)
            {
                break;
            }

            if (currentId == endWaypointId)
            {
                break;
            }

            unvisited.Remove(currentId);
            if (!_links.TryGetValue(currentId, out var neighbors))
            {
                continue;
            }

            foreach (var neighborId in neighbors)
            {
                if (!unvisited.Contains(neighborId))
                {
                    continue;
                }

                var currentWaypoint = _waypoints.First(waypoint => waypoint.Id == currentId);
                var neighborWaypoint = _waypoints.First(waypoint => waypoint.Id == neighborId);
                var edgeCost = Vector3.Distance(currentWaypoint.Position, neighborWaypoint.Position);
                var alternativeDistance = distances[currentId] + edgeCost;
                if (alternativeDistance < distances[neighborId])
                {
                    distances[neighborId] = alternativeDistance;
                    previous[neighborId] = currentId;
                }
            }
        }

        if (previous[endWaypointId] == null)
        {
            return false;
        }

        var pathStack = new Stack<int>();
        var cursor = endWaypointId;
        pathStack.Push(cursor);
        while (true)
        {
            var previousWaypointId = previous[cursor];
            if (!previousWaypointId.HasValue)
            {
                break;
            }

            cursor = previousWaypointId.Value;
            pathStack.Push(cursor);
        }

        path = pathStack.ToList();
        return path.Count > 0;
    }

    private NavigationWaypointData? FindNearestWaypoint(Vector3 position)
    {
        NavigationWaypointData? nearest = null;
        var bestDistance = float.MaxValue;
        foreach (var waypoint in _waypoints)
        {
            var distance = Vector3.Distance(position, waypoint.Position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = waypoint;
            }
        }

        return nearest;
    }

    private void UpdateWaypointSelectionVisuals()
    {
        foreach (var marker in _markers.Values)
        {
            marker.SetSelected(marker.WaypointId == PendingLinkStartWaypointId);
        }
    }

    private void OnRegistryModeChanged(bool isEnabled)
    {
        _visualsVisible = isEnabled;
        foreach (var marker in _markers.Values)
        {
            marker.SetVisualizationVisible(isEnabled);
        }

        foreach (var lineRenderer in _linkRenderers.Values)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = isEnabled;
            }
        }
    }

    private bool TryGetTargetWaypoint(out NavigationWaypointData targetWaypoint)
    {
        var activeCamera = Camera.main;
        if (activeCamera != null)
        {
            var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
            var hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide).OrderBy(hit => hit.distance).ToArray();
            foreach (var hitInfo in hits)
            {
                var marker = hitInfo.collider.GetComponentInParent<WyrdrasilNavigationWaypointMarker>();
                if (marker == null)
                {
                    continue;
                }

                var waypoint = _waypoints.FirstOrDefault(candidate => candidate.Id == marker.WaypointId);
                if (waypoint != null)
                {
                    targetWaypoint = waypoint;
                    return true;
                }
            }
        }

        targetWaypoint = null!;
        return false;
    }

    private void CreateWaypointWorldObject(NavigationWaypointData waypointData)
    {
        var root = new GameObject($"Wyrdrasil_Waypoint_{waypointData.Id}");
        root.transform.position = waypointData.Position;
        _waypointRoots[waypointData.Id] = root;

        var marker = root.AddComponent<WyrdrasilNavigationWaypointMarker>();
        marker.Initialize(waypointData.Id);

        var interactionCollider = root.AddComponent<SphereCollider>();
        interactionCollider.isTrigger = true;
        interactionCollider.radius = 0.55f;
        marker.RegisterCollider(interactionCollider);

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(0.35f, 0.15f, 0.35f);
        var visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            visualCollider.isTrigger = true;
            marker.RegisterCollider(visualCollider);
        }

        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = CreateWaypointMaterial(false);
            if (material != null)
            {
                renderer.material = material;
            }
        }

        marker.RegisterRenderer(renderer);
        marker.SetVisualizationVisible(_visualsVisible);
        _markers[waypointData.Id] = marker;
    }

    private void CreateLinkVisual(int waypointAId, int waypointBId)
    {
        var waypointA = _waypoints.First(waypoint => waypoint.Id == waypointAId);
        var waypointB = _waypoints.First(waypoint => waypoint.Id == waypointBId);
        var linkKey = GetLinkKey(waypointAId, waypointBId);
        if (_linkRenderers.ContainsKey(linkKey))
        {
            return;
        }

        var root = new GameObject($"Wyrdrasil_WaypointLink_{linkKey}");
        var lineRenderer = root.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = 0.04f;
        lineRenderer.endWidth = 0.04f;
        lineRenderer.material = CreateLinkMaterial();
        lineRenderer.startColor = new Color(0.2f, 1f, 0.65f, 1f);
        lineRenderer.endColor = new Color(0.2f, 1f, 0.65f, 1f);
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.SetPosition(0, waypointA.Position + new Vector3(0f, 0.08f, 0f));
        lineRenderer.SetPosition(1, waypointB.Position + new Vector3(0f, 0.08f, 0f));
        lineRenderer.enabled = _visualsVisible;
        _linkRenderers[linkKey] = lineRenderer;
    }

    private static string GetLinkKey(int waypointAId, int waypointBId)
    {
        return waypointAId < waypointBId ? $"{waypointAId}-{waypointBId}" : $"{waypointBId}-{waypointAId}";
    }

    private static Material? CreateWaypointMaterial(bool isSelected)
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (!shader)
        {
            return null;
        }

        var material = new Material(shader)
        {
            color = isSelected ? new Color(0.2f, 1f, 0.65f, 1f) : new Color(1f, 0.45f, 0.2f, 1f)
        };
        return material;
    }

    private static Material CreateLinkMaterial()
    {
        var shader = Shader.Find("Sprites/Default");
        if (!shader)
        {
            shader = Shader.Find("Unlit/Color");
        }

        return new Material(shader);
    }
}
