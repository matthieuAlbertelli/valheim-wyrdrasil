using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Wyrdrasil.Construction.Models;
using Wyrdrasil.Construction.Services;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Souls.Services;

namespace Wyrdrasil.Registry.Services;

public sealed class ConstructionLinkVisualService
{
    private sealed class LinkVisual
    {
        public GameObject Root { get; }
        public List<LineRenderer> DashRenderers { get; } = new();

        public LinkVisual(string name, Material material)
        {
            Root = new GameObject(name);
            Root.layer = LayerMask.NameToLayer("Ignore Raycast");

            for (var i = 0; i < 12; i++)
            {
                var dashObject = new GameObject($"Dash_{i}");
                dashObject.transform.SetParent(Root.transform, false);
                dashObject.layer = LayerMask.NameToLayer("Ignore Raycast");

                var renderer = dashObject.AddComponent<LineRenderer>();
                renderer.useWorldSpace = true;
                renderer.loop = false;
                renderer.positionCount = 2;
                renderer.startWidth = 0.06f;
                renderer.endWidth = 0.06f;
                renderer.material = new Material(material);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                DashRenderers.Add(renderer);
            }
        }

        public void SetActive(bool isActive)
        {
            if (Root != null)
            {
                Root.SetActive(isActive);
            }
        }

        public void Destroy()
        {
            foreach (var dashRenderer in DashRenderers)
            {
                if (dashRenderer != null && dashRenderer.material != null)
                {
                    Object.Destroy(dashRenderer.material);
                }
            }

            if (Root != null)
            {
                Object.Destroy(Root);
            }
        }
    }

    private readonly ConstructionProjectService _constructionProjectService;
    private readonly ConstructionProjectMarkerService _constructionProjectMarkerService;
    private readonly CraftStationService _craftStationService;
    private readonly ResidentRuntimeService _residentRuntimeService;
    private readonly Dictionary<string, LinkVisual> _persistentLinks = new();
    private readonly Material _lineMaterialTemplate;

    private int? _hoveredWorkbenchProjectId;
    private int? _hoveredWorkbenchCraftStationId;
    private int? _hoveredResidentProjectId;
    private int? _hoveredResidentId;

    public ConstructionLinkVisualService(
        ConstructionProjectService constructionProjectService,
        ConstructionProjectMarkerService constructionProjectMarkerService,
        CraftStationService craftStationService,
        ResidentRuntimeService residentRuntimeService)
    {
        _constructionProjectService = constructionProjectService;
        _constructionProjectMarkerService = constructionProjectMarkerService;
        _craftStationService = craftStationService;
        _residentRuntimeService = residentRuntimeService;

        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        _lineMaterialTemplate = new Material(shader)
        {
            name = "WyrdrasilConstructionLinkTemplate"
        };
    }

    public void SetHoveredWorkbenchLink(int? projectId, int? craftStationId)
    {
        _hoveredWorkbenchProjectId = projectId;
        _hoveredWorkbenchCraftStationId = craftStationId;
    }

    public void SetHoveredResidentLink(int? projectId, int? residentId)
    {
        _hoveredResidentProjectId = projectId;
        _hoveredResidentId = residentId;
    }

    public void Update()
    {
        UpdatePersistentLinks();
        UpdateHoverWorkbenchLink();
        UpdateHoverResidentLink();
    }

    public void Reset()
    {
        foreach (var link in _persistentLinks.Values)
        {
            link.Destroy();
        }

        _persistentLinks.Clear();
        DestroyLink("HoverWorkbench");
        DestroyLink("HoverResident");

        if (_lineMaterialTemplate != null)
        {
            Object.Destroy(_lineMaterialTemplate);
        }
    }

    private void UpdatePersistentLinks()
    {
        var desiredKeys = new HashSet<string>();

        foreach (var project in _constructionProjectService.Projects)
        {
            if (project.State == ConstructionProjectState.Completed)
            {
                continue;
            }

            var start = GetProjectLinkOrigin(project.Id, project.OriginPosition);
            var workPost = project.WorkPosts.FirstOrDefault();
            if (workPost != null && workPost.CraftStationId > 0 &&
                TryGetCraftStationLinkPoint(workPost.CraftStationId, out var craftStationPoint))
            {
                var key = $"ProjectWorkbench_{project.Id}";
                desiredKeys.Add(key);
                UpdateLink(key, start, craftStationPoint, new Color(1f, 0.8f, 0.2f, 0.95f));
            }

            if (workPost != null && workPost.AssignedResidentId.HasValue &&
                TryGetResidentLinkPoint(workPost.AssignedResidentId.Value, out var residentPoint))
            {
                var key = $"ProjectResident_{project.Id}";
                desiredKeys.Add(key);
                UpdateLink(key, start, residentPoint, new Color(0.35f, 1f, 0.85f, 0.95f));
            }
        }

        foreach (var staleKey in _persistentLinks.Keys.Where(key => !desiredKeys.Contains(key)).ToList())
        {
            _persistentLinks[staleKey].Destroy();
            _persistentLinks.Remove(staleKey);
        }
    }

    private void UpdateHoverWorkbenchLink()
    {
        if (_hoveredWorkbenchProjectId.HasValue && _hoveredWorkbenchCraftStationId.HasValue &&
            _constructionProjectService.TryGetProject(_hoveredWorkbenchProjectId.Value, out var project) &&
            TryGetCraftStationLinkPoint(_hoveredWorkbenchCraftStationId.Value, out var craftStationPoint))
        {
            UpdateLink(
                "HoverWorkbench",
                GetProjectLinkOrigin(project.Id, project.OriginPosition),
                craftStationPoint,
                new Color(1f, 0.95f, 0.35f, 1f),
                isPersistent: false);
            return;
        }

        DestroyLink("HoverWorkbench", isPersistent: false);
    }

    private void UpdateHoverResidentLink()
    {
        if (_hoveredResidentProjectId.HasValue && _hoveredResidentId.HasValue &&
            _constructionProjectService.TryGetProject(_hoveredResidentProjectId.Value, out var project) &&
            TryGetResidentLinkPoint(_hoveredResidentId.Value, out var residentPoint))
        {
            UpdateLink(
                "HoverResident",
                GetProjectLinkOrigin(project.Id, project.OriginPosition),
                residentPoint,
                new Color(1f, 0.75f, 0.2f, 1f),
                isPersistent: false);
            return;
        }

        DestroyLink("HoverResident", isPersistent: false);
    }

    private Vector3 GetProjectLinkOrigin(int projectId, Vector3 originPosition)
    {
        return originPosition + new Vector3(0f, 3.8f, 0f);
    }

    private bool TryGetCraftStationLinkPoint(int craftStationId, out Vector3 point)
    {
        if (_craftStationService.TryGetCraftStationById(craftStationId, out var craftStation))
        {
            if (craftStation.TryResolveWorldAnchor(out var anchorWorldPosition, out _))
            {
                point = anchorWorldPosition + new Vector3(0f, 0.12f, 0f);
                return true;
            }

            point = craftStation.ReferenceWorldPosition + new Vector3(0f, 0.9f, 0f);
            return true;
        }

        point = default;
        return false;
    }

    private bool TryGetResidentLinkPoint(int residentId, out Vector3 point)
    {
        if (_residentRuntimeService.TryGetBoundCharacter(residentId, out var character) && character != null)
        {
            point = character.transform.position + new Vector3(0f, 1.4f, 0f);
            return true;
        }

        point = default;
        return false;
    }

    private void UpdateLink(string key, Vector3 start, Vector3 end, Color color, bool isPersistent = true)
    {
        var link = GetOrCreateLink(key, isPersistent);
        link.SetActive(true);

        var dashCount = link.DashRenderers.Count;
        var dashFill = 0.55f;

        for (var i = 0; i < dashCount; i++)
        {
            var renderer = link.DashRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            var startT = i / (float)dashCount;
            var endT = startT + (dashFill / dashCount);
            if (endT > 1f)
            {
                endT = 1f;
            }

            renderer.SetPosition(0, Vector3.Lerp(start, end, startT));
            renderer.SetPosition(1, Vector3.Lerp(start, end, endT));
            renderer.startColor = color;
            renderer.endColor = color;
        }
    }

    private LinkVisual GetOrCreateLink(string key, bool isPersistent)
    {
        if (isPersistent)
        {
            if (_persistentLinks.TryGetValue(key, out var link))
            {
                return link;
            }

            link = new LinkVisual($"WyrdrasilConstructionLink_{key}", _lineMaterialTemplate);
            _persistentLinks[key] = link;
            return link;
        }

        if (_persistentLinks.TryGetValue(key, out var hoverLink))
        {
            return hoverLink;
        }

        hoverLink = new LinkVisual($"WyrdrasilConstructionLink_{key}", _lineMaterialTemplate);
        _persistentLinks[key] = hoverLink;
        return hoverLink;
    }

    private void DestroyLink(string key, bool isPersistent = false)
    {
        if (_persistentLinks.TryGetValue(key, out var link))
        {
            link.Destroy();
            _persistentLinks.Remove(key);
        }
    }
}
