using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Models;

namespace Wyrdrasil.Construction.Services;

public sealed class ConstructionProjectMarkerService
{
    private readonly ConstructionProjectService _constructionProjectService;
    private readonly Dictionary<int, GameObject> _markersByProjectId = new();
    private Material? _markerMaterial;
    private int? _hoveredProjectId;

    public ConstructionProjectMarkerService(ConstructionProjectService constructionProjectService)
    {
        _constructionProjectService = constructionProjectService;
    }

    public void Update()
    {
        var visibleProjects = _constructionProjectService.Projects
            .Where(project => project.State != ConstructionProjectState.Completed)
            .ToList();

        var visibleIds = new HashSet<int>(visibleProjects.Select(project => project.Id));
        foreach (var staleId in _markersByProjectId.Keys.Where(id => !visibleIds.Contains(id)).ToList())
        {
            if (_markersByProjectId.TryGetValue(staleId, out var staleMarker) && staleMarker != null)
            {
                Object.Destroy(staleMarker);
            }

            _markersByProjectId.Remove(staleId);
        }

        foreach (var project in visibleProjects)
        {
            if (!_markersByProjectId.TryGetValue(project.Id, out var marker) || marker == null)
            {
                marker = CreateMarker(project.Id);
                _markersByProjectId[project.Id] = marker;
            }

            marker.transform.position = project.OriginPosition;
            marker.transform.rotation = Quaternion.identity;
            ApplyMarkerVisual(project.Id, marker);
        }
    }


    public bool TryGetTargetedProjectId(out int projectId)
    {
        projectId = 0;
        var camera = Camera.main;
        if (camera == null || _markersByProjectId.Count == 0)
        {
            return false;
        }

        var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var bestDistance = 72f;
        var found = false;

        foreach (var entry in _markersByProjectId)
        {
            if (entry.Value == null)
            {
                continue;
            }

            var screenPoint = camera.WorldToScreenPoint(entry.Value.transform.position + new Vector3(0f, 3.8f, 0f));
            if (screenPoint.z <= 0f)
            {
                continue;
            }

            var distance = Vector2.Distance(screenCenter, new Vector2(screenPoint.x, screenPoint.y));
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            projectId = entry.Key;
            found = true;
        }

        return found;
    }


    public void SetHoveredProject(int? projectId)
    {
        _hoveredProjectId = projectId;
    }
    public void Reset()
    {
        foreach (var marker in _markersByProjectId.Values)
        {
            if (marker != null)
            {
                Object.Destroy(marker);
            }
        }

        _markersByProjectId.Clear();

        if (_markerMaterial != null)
        {
            Object.Destroy(_markerMaterial);
            _markerMaterial = null;
        }
    }

    private GameObject CreateMarker(int projectId)
    {
        var root = new GameObject($"WyrdrasilConstructionProjectMarker_{projectId}");
        root.layer = LayerMask.NameToLayer("Ignore Raycast");

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 1.75f, 0f);
        shaft.transform.localScale = new Vector3(0.18f, 1.75f, 0.18f);
        ConfigurePrimitive(shaft);

        var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        beacon.name = "Beacon";
        beacon.transform.SetParent(root.transform, false);
        beacon.transform.localPosition = new Vector3(0f, 3.8f, 0f);
        beacon.transform.localScale = Vector3.one * 0.45f;
        ConfigurePrimitive(beacon);

        return root;
    }

    private void ApplyMarkerVisual(int projectId, GameObject root)
    {
        var isHovered = _hoveredProjectId.HasValue && _hoveredProjectId.Value == projectId;
        var baseColor = isHovered
            ? new Color(1f, 0.95f, 0.35f, 0.95f)
            : new Color(1f, 0.55f, 0.15f, 0.90f);
        var emissionColor = isHovered
            ? new Color(0.6f, 0.45f, 0.05f, 1f)
            : new Color(0.45f, 0.18f, 0.02f, 1f);

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            var material = renderer.material;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_Color"))
            {
                material.color = baseColor;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissionColor);
            }
        }
    }

    private void ConfigurePrimitive(GameObject primitive)
    {
        var collider = primitive.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        if (primitive.TryGetComponent<Renderer>(out var renderer))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = new Material(GetMarkerMaterial());
        }

        primitive.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    private Material GetMarkerMaterial()
    {
        if (_markerMaterial != null)
        {
            return _markerMaterial;
        }

        var shader = Shader.Find("Unlit/Color")
                     ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Sprites/Default");

        _markerMaterial = new Material(shader)
        {
            name = "WyrdrasilConstructionProjectMarkerMaterial"
        };

        var color = new Color(1f, 0.55f, 0.15f, 0.90f);
        if (_markerMaterial.HasProperty("_Color"))
        {
            _markerMaterial.color = color;
        }

        if (_markerMaterial.HasProperty("_BaseColor"))
        {
            _markerMaterial.SetColor("_BaseColor", color);
        }

        if (_markerMaterial.HasProperty("_EmissionColor"))
        {
            _markerMaterial.SetColor("_EmissionColor", new Color(0.45f, 0.18f, 0.02f, 1f));
        }

        return _markerMaterial;
    }
}
