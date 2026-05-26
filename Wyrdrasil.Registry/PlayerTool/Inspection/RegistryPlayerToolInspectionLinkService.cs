using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Construction.Runtime;
using Wyrdrasil.Core.Tool;
using Wyrdrasil.Settlements.Components;
using Wyrdrasil.Settlements.Runtime;
using Wyrdrasil.Settlements.Tool;
using Wyrdrasil.Souls.Runtime;
using Wyrdrasil.Souls.Tool;
using Object = UnityEngine.Object;

namespace Wyrdrasil.Registry.PlayerTool.Inspection;

/// <summary>
/// Draws animated inspect-mode relationship links between Wyrdrasil entities.
///
/// This service is intentionally data-driven from runtime APIs instead of hard-coding
/// the current player-tool actions. New assignment target kinds can be displayed by
/// adding one resolver branch here without changing the reveal/marker scanner.
/// </summary>
public sealed class RegistryPlayerToolInspectionLinkService
{
    private sealed class LinkVisual
    {
        public string Key { get; }
        public GameObject Root { get; }
        public LineRenderer OuterLine { get; }
        public LineRenderer InnerLine { get; }
        public LineRenderer TargetOuterRing { get; }
        public LineRenderer TargetInnerRing { get; }
        public WyrdrasilFurnitureGlowOverlay TargetFurnitureGlow { get; }
        public float PhaseOffset { get; }
        public int? CurrentGlowRootInstanceId { get; set; }

        public LinkVisual(
            string key,
            GameObject root,
            LineRenderer outerLine,
            LineRenderer innerLine,
            LineRenderer targetOuterRing,
            LineRenderer targetInnerRing,
            WyrdrasilFurnitureGlowOverlay targetFurnitureGlow,
            float phaseOffset)
        {
            Key = key;
            Root = root;
            OuterLine = outerLine;
            InnerLine = innerLine;
            TargetOuterRing = targetOuterRing;
            TargetInnerRing = targetInnerRing;
            TargetFurnitureGlow = targetFurnitureGlow;
            PhaseOffset = phaseOffset;
        }
    }

    private sealed class LinkTarget
    {
        public Vector3 Point { get; }
        public float RingY { get; }
        public float RadiusX { get; }
        public float RadiusZ { get; }
        public GameObject? FurnitureRoot { get; }
        public bool UseFurnitureGlow { get; }
        public Color LinkColor { get; }

        public LinkTarget(
            Vector3 point,
            float ringY,
            float radiusX,
            float radiusZ,
            GameObject? furnitureRoot = null,
            bool useFurnitureGlow = false,
            Color? linkColor = null)
        {
            Point = point;
            RingY = ringY;
            RadiusX = radiusX;
            RadiusZ = radiusZ;
            FurnitureRoot = furnitureRoot;
            UseFurnitureGlow = useFurnitureGlow;
            LinkColor = linkColor ?? WyrdrasilVisualizationPalette.AssignedPurple;
        }
    }

    private readonly ISoulsRuntimeApi _soulsRuntimeApi;
    private readonly ISettlementsRuntimeApi _settlementsRuntimeApi;
    private readonly IConstructionRuntimeApi _constructionRuntimeApi;
    private readonly Dictionary<string, LinkVisual> _linksByKey = new();
    private readonly Material _outerMaterial;
    private readonly Material _innerMaterial;
    private bool _isVisible;
    private float _nextRefreshTime;

    private const int CurvePointCount = 32;
    private const int TargetRingPointCount = 56;
    private const float RefreshIntervalSeconds = 0.35f;
    private const float OuterWidth = 0.085f;
    private const float InnerWidth = 0.03f;
    private const float TargetOuterWidth = 0.07f;
    private const float TargetInnerWidth = 0.025f;
    private const float WaveAmplitude = 0.16f;
    private const float WaveFrequency = 2.4f;
    private const float PulseSpeed = 1.55f;

    public RegistryPlayerToolInspectionLinkService(
        ISoulsRuntimeApi soulsRuntimeApi,
        ISettlementsRuntimeApi settlementsRuntimeApi,
        IConstructionRuntimeApi constructionRuntimeApi)
    {
        _soulsRuntimeApi = soulsRuntimeApi;
        _settlementsRuntimeApi = settlementsRuntimeApi;
        _constructionRuntimeApi = constructionRuntimeApi;
        _outerMaterial = CreateLineMaterial(WyrdrasilVisualizationPalette.AssignedPurpleFaint, "WyrdrasilInspectionLinkOuter");
        _innerMaterial = CreateLineMaterial(WyrdrasilVisualizationPalette.AssignedPurpleSoft, "WyrdrasilInspectionLinkInner");
    }

    public void SetVisible(bool visible)
    {
        if (_isVisible == visible)
        {
            if (visible)
            {
                RefreshLinksIfDue(force: false);
                UpdateVisualCurves();
            }

            return;
        }

        _isVisible = visible;
        _nextRefreshTime = 0f;

        if (!visible)
        {
            Reset();
            return;
        }

        RefreshLinksIfDue(force: true);
        UpdateVisualCurves();
    }

    public void Update()
    {
        if (!_isVisible)
        {
            return;
        }

        RefreshLinksIfDue(force: false);
        UpdateVisualCurves();
    }

    public void Reset()
    {
        foreach (var link in _linksByKey.Values)
        {
            if (link.Root != null)
            {
                Object.Destroy(link.Root);
            }
        }

        _linksByKey.Clear();
    }

    private void RefreshLinksIfDue(bool force)
    {
        if (!force && Time.unscaledTime < _nextRefreshTime)
        {
            return;
        }

        _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
        RefreshLinks();
    }

    private void RefreshLinks()
    {
        var expectedKeys = new HashSet<string>();

        foreach (var resident in _soulsRuntimeApi.RegisteredNpcs)
        {
            if (!TryResolveResidentLinkPoint(resident, out _))
            {
                continue;
            }

            foreach (var assignment in resident.Assignments)
            {
                if (!TryResolveAssignmentLinkTarget(assignment.Target, out _))
                {
                    continue;
                }

                var key = BuildLinkKey(resident.Id, assignment.Purpose, assignment.Target);
                expectedKeys.Add(key);
                if (_linksByKey.ContainsKey(key))
                {
                    continue;
                }

                _linksByKey[key] = CreateLinkVisual(key);
            }
        }

        foreach (var staleKey in _linksByKey.Keys.Where(key => !expectedKeys.Contains(key)).ToList())
        {
            if (_linksByKey.TryGetValue(staleKey, out var staleLink) && staleLink.Root != null)
            {
                Object.Destroy(staleLink.Root);
            }

            _linksByKey.Remove(staleKey);
        }
    }

    private void UpdateVisualCurves()
    {
        foreach (var pair in _linksByKey.ToList())
        {
            if (!TryParseLinkKey(pair.Key, out var residentId, out var targetKind, out var targetId) ||
                !_soulsRuntimeApi.TryGetResidentById(residentId, out var resident) ||
                !TryResolveResidentLinkPoint(resident, out var start) ||
                !TryResolveAssignmentLinkTarget(new OccupationTargetRef(targetKind, targetId), out var target))
            {
                if (pair.Value.Root != null)
                {
                    Object.Destroy(pair.Value.Root);
                }

                _linksByKey.Remove(pair.Key);
                continue;
            }

            UpdateCurve(pair.Value, start, target);
        }
    }

    private bool TryResolveResidentLinkPoint(RegisteredNpcData resident, out Vector3 point)
    {
        if (_soulsRuntimeApi.TryGetBoundCharacter(resident.Id, out var character) && character != null)
        {
            point = character.transform.position + Vector3.up * 1.35f;
            return true;
        }

        if (resident.PresenceSnapshot.ShouldRespawnOnLoad)
        {
            point = resident.PresenceSnapshot.WorldPosition + Vector3.up * 1.35f;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private bool TryResolveAssignmentLinkTarget(OccupationTargetRef target, out LinkTarget linkTarget)
    {
        switch (target.TargetKind)
        {
            case OccupationTargetKind.Slot:
                foreach (var slot in _settlementsRuntimeApi.Slots)
                {
                    if (slot.Id == target.TargetId)
                    {
                        var point = slot.Position + Vector3.up * 0.55f;
                        linkTarget = new LinkTarget(point, slot.Position.y + 0.06f, 0.38f, 0.38f);
                        return true;
                    }
                }

                break;

            case OccupationTargetKind.Seat:
                if (_settlementsRuntimeApi.TryGetSeatById(target.TargetId, out var seat))
                {
                    linkTarget = ResolveFurnitureTarget(seat.FurnitureRoot, seat.SeatPosition, 0.55f, 0.45f, useFurnitureGlow: false);
                    return true;
                }

                break;

            case OccupationTargetKind.Bed:
                foreach (var bed in _settlementsRuntimeApi.Beds)
                {
                    if (bed.Id == target.TargetId)
                    {
                        linkTarget = ResolveFurnitureTarget(bed.FurnitureRoot, bed.SleepPosition, 0.55f, 0.55f, useFurnitureGlow: false);
                        return true;
                    }
                }

                break;

            case OccupationTargetKind.CraftStation:
                if (_settlementsRuntimeApi.TryGetCraftStationById(target.TargetId, out var craftStation))
                {
                    // Inspection must target the furniture itself, not the NPC interaction anchor.
                    // The anchor is where the viking stands to work; using it here makes the relation
                    // look like it points to an invisible spot instead of the actual workbench.
                    linkTarget = ResolveFurnitureTarget(craftStation.FurnitureRoot, craftStation.ReferenceWorldPosition, 0.75f, 0.70f, useFurnitureGlow: true);
                    return true;
                }

                break;

            case OccupationTargetKind.ConstructionWorkPost:
                if (_constructionRuntimeApi.TryGetWorkPost(target.TargetId, out var workPost) &&
                    _constructionRuntimeApi.TryGetProject(workPost.ProjectId, out var project))
                {
                    var point = project.OriginPosition + Vector3.up * 3.8f;
                    linkTarget = new LinkTarget(
                        point,
                        project.OriginPosition.y + 0.08f,
                        0.95f,
                        0.95f,
                        linkColor: WyrdrasilVisualizationPalette.ConstructionOrange);
                    return true;
                }

                break;
        }

        linkTarget = null!;
        return false;
    }

    private static LinkTarget ResolveFurnitureTarget(GameObject? furnitureRoot, Vector3 fallback, float verticalOffset, float minimumRadius, bool useFurnitureGlow)
    {
        if (furnitureRoot == null)
        {
            return new LinkTarget(fallback + Vector3.up * verticalOffset, fallback.y + 0.06f, minimumRadius, minimumRadius);
        }

        var renderers = furnitureRoot.GetComponentsInChildren<Renderer>(true);
        var hasBounds = false;
        var bounds = new Bounds(furnitureRoot.transform.position, Vector3.zero);

        foreach (var renderer in renderers)
        {
            if (!IsEligibleTargetRenderer(renderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return new LinkTarget(furnitureRoot.transform.position + Vector3.up * verticalOffset, furnitureRoot.transform.position.y + 0.06f, minimumRadius, minimumRadius, furnitureRoot, useFurnitureGlow);
        }

        var point = bounds.center + Vector3.up * Mathf.Max(0.15f, bounds.extents.y * 0.35f);
        var radiusX = Mathf.Max(bounds.extents.x + 0.18f, minimumRadius);
        var radiusZ = Mathf.Max(bounds.extents.z + 0.18f, minimumRadius);
        var ringY = bounds.min.y + 0.05f;
        return new LinkTarget(point, ringY, radiusX, radiusZ, furnitureRoot, useFurnitureGlow);
    }

    private LinkVisual CreateLinkVisual(string key)
    {
        var root = new GameObject($"WyrdrasilInspectionLink_{key}");
        root.hideFlags = HideFlags.DontSave;

        var outer = CreateLineRenderer(root, "Outer", _outerMaterial, OuterWidth, WyrdrasilVisualizationPalette.AssignedPurpleFaint, CurvePointCount, loop: false);
        var inner = CreateLineRenderer(root, "Inner", _innerMaterial, InnerWidth, WyrdrasilVisualizationPalette.AssignedPurpleSoft, CurvePointCount, loop: false);
        var targetOuter = CreateLineRenderer(root, "TargetOuterRing", _outerMaterial, TargetOuterWidth, WyrdrasilVisualizationPalette.AssignedPurpleFaint, TargetRingPointCount, loop: true);
        var targetInner = CreateLineRenderer(root, "TargetInnerRing", _innerMaterial, TargetInnerWidth, WyrdrasilVisualizationPalette.AssignedPurpleSoft, TargetRingPointCount, loop: true);
        var targetFurnitureGlow = CreateTargetFurnitureGlow(root);
        return new LinkVisual(key, root, outer, inner, targetOuter, targetInner, targetFurnitureGlow, ResolvePhaseOffset(key));
    }

    private static LineRenderer CreateLineRenderer(GameObject root, string name, Material material, float width, Color color, int pointCount, bool loop)
    {
        var child = new GameObject(name);
        child.hideFlags = HideFlags.DontSave;
        child.transform.SetParent(root.transform, false);

        var line = child.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.positionCount = pointCount;
        line.startWidth = width;
        line.endWidth = width;
        line.material = material;
        line.startColor = color;
        line.endColor = color;
        line.numCornerVertices = 5;
        line.numCapVertices = 5;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private static WyrdrasilFurnitureGlowOverlay CreateTargetFurnitureGlow(GameObject root)
    {
        var child = new GameObject("TargetFurnitureMeshGlow");
        child.hideFlags = HideFlags.DontSave;
        child.transform.SetParent(root.transform, false);
        return child.AddComponent<WyrdrasilFurnitureGlowOverlay>();
    }

    private static void UpdateCurve(LinkVisual link, Vector3 start, LinkTarget target)
    {
        var end = target.Point;
        var delta = end - start;
        var distance = Mathf.Max(0.1f, delta.magnitude);
        var forward = delta / distance;
        var sideways = Vector3.Cross(Vector3.up, forward);
        if (sideways.sqrMagnitude <= 0.0001f)
        {
            sideways = Vector3.right;
        }

        sideways.Normalize();

        var arcHeight = Mathf.Clamp(distance * 0.23f, 0.7f, 3.3f);
        var sideOffset = Mathf.Clamp(distance * 0.10f, 0.35f, 1.4f);
        var control = (start + end) * 0.5f + Vector3.up * arcHeight + sideways * sideOffset;
        var phase = (Time.unscaledTime * PulseSpeed) + link.PhaseOffset;

        for (var index = 0; index < CurvePointCount; index++)
        {
            var t = index / (CurvePointCount - 1f);
            var curve = QuadraticBezier(start, control, end, t);
            var waveEnvelope = Mathf.Sin(t * Mathf.PI);
            var wave = Mathf.Sin((t * WaveFrequency * Mathf.PI * 2f) + phase) * WaveAmplitude * waveEnvelope;
            var breathe = Mathf.Sin((phase * 1.9f) + (t * Mathf.PI * 2f)) * 0.055f * waveEnvelope;
            var point = curve + sideways * wave + Vector3.up * breathe;
            link.OuterLine.SetPosition(index, point);
            link.InnerLine.SetPosition(index, point);
        }

        var alphaPulse = 0.48f + (Mathf.Sin(phase * 1.35f) * 0.10f);
        var baseColor = target.LinkColor;
        var outerColor = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            Mathf.Clamp(alphaPulse * 0.55f, 0.18f, 0.45f));
        var innerColor = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            Mathf.Clamp(alphaPulse + 0.18f, 0.42f, 0.82f));

        link.OuterLine.startColor = outerColor;
        link.OuterLine.endColor = outerColor;
        link.InnerLine.startColor = innerColor;
        link.InnerLine.endColor = innerColor;
        UpdateTargetRing(link, target, outerColor, innerColor, phase);
    }

    private static void UpdateTargetRing(LinkVisual link, LinkTarget target, Color outerColor, Color innerColor, float phase)
    {
        var radiusPulse = 1f + Mathf.Sin(phase * 1.7f) * 0.045f;
        var outerRingColor = new Color(outerColor.r, outerColor.g, outerColor.b, Mathf.Clamp01(outerColor.a + 0.20f));
        var innerRingColor = new Color(innerColor.r, innerColor.g, innerColor.b, Mathf.Clamp01(innerColor.a + 0.08f));

        link.TargetOuterRing.startColor = outerRingColor;
        link.TargetOuterRing.endColor = outerRingColor;
        link.TargetInnerRing.startColor = innerRingColor;
        link.TargetInnerRing.endColor = innerRingColor;

        link.TargetOuterRing.startWidth = TargetOuterWidth;
        link.TargetOuterRing.endWidth = TargetOuterWidth;
        link.TargetInnerRing.startWidth = TargetInnerWidth;
        link.TargetInnerRing.endWidth = TargetInnerWidth;

        ApplyTargetRing(link.TargetOuterRing, target, radiusPulse);
        ApplyTargetRing(link.TargetInnerRing, target, radiusPulse * 0.86f);
        UpdateTargetFurnitureGlow(link, target, innerRingColor);
    }

    private static void UpdateTargetFurnitureGlow(LinkVisual link, LinkTarget target, Color color)
    {
        if (!target.UseFurnitureGlow || target.FurnitureRoot == null || link.TargetFurnitureGlow == null)
        {
            link.TargetFurnitureGlow?.SetVisible(false, color, emphasized: false);
            link.CurrentGlowRootInstanceId = null;
            return;
        }

        var rootInstanceId = target.FurnitureRoot.GetInstanceID();
        if (link.CurrentGlowRootInstanceId != rootInstanceId)
        {
            link.TargetFurnitureGlow.Initialize(target.FurnitureRoot.transform);
            link.TargetFurnitureGlow.RegisterRenderers(target.FurnitureRoot.GetComponentsInChildren<Renderer>(true));
            link.CurrentGlowRootInstanceId = rootInstanceId;
        }

        link.TargetFurnitureGlow.SetVisible(true, color, emphasized: true);
    }

    private static void ApplyTargetRing(LineRenderer ring, LinkTarget target, float radiusScale)
    {
        for (var index = 0; index < TargetRingPointCount; index++)
        {
            var angle = (index / (float)TargetRingPointCount) * Mathf.PI * 2f;
            ring.SetPosition(index, new Vector3(
                target.Point.x + Mathf.Cos(angle) * target.RadiusX * radiusScale,
                target.RingY,
                target.Point.z + Mathf.Sin(angle) * target.RadiusZ * radiusScale));
        }
    }

    private static bool IsEligibleTargetRenderer(Renderer? renderer)
    {
        if (renderer == null || renderer is LineRenderer)
        {
            return false;
        }

        var current = renderer.transform;
        while (current != null)
        {
            if (current.name.StartsWith("Wyrdrasil_", StringComparison.Ordinal))
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
    {
        var inverse = 1f - t;
        return (inverse * inverse * start) + (2f * inverse * t * control) + (t * t * end);
    }

    private static string BuildLinkKey(int residentId, ResidentAssignmentPurpose purpose, OccupationTargetRef target)
    {
        return $"{residentId}|{(int)purpose}|{(int)target.TargetKind}|{target.TargetId}";
    }

    private static bool TryParseLinkKey(string key, out int residentId, out OccupationTargetKind targetKind, out int targetId)
    {
        residentId = default;
        targetKind = default;
        targetId = default;

        var parts = key.Split('|');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out residentId) ||
            !int.TryParse(parts[2], out var targetKindValue) ||
            !int.TryParse(parts[3], out targetId))
        {
            return false;
        }

        targetKind = (OccupationTargetKind)targetKindValue;
        return true;
    }

    private static float ResolvePhaseOffset(string key)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in key)
            {
                hash = (hash * 31) + character;
            }

            return Mathf.Abs(hash % 628) / 100f;
        }
    }

    private static Material CreateLineMaterial(Color color, string name)
    {
        var shader = Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                     ?? Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };

        if (material.HasProperty("_Color"))
        {
            material.color = color;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.65f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }
}
