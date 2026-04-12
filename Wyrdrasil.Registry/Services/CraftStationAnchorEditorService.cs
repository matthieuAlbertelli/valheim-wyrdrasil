using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class CraftStationAnchorEditorService
{
    private const string UsePointName = "Wyrdrasil_CraftStationUsePoint";
    private const string ApproachPointName = "Wyrdrasil_CraftStationApproachPoint";
    private const float DefaultMoveStep = 0.06f;
    private const float FineMoveStep = 0.015f;
    private const float DefaultRotationStep = 4f;
    private const float FineRotationStep = 1f;

    private readonly ManualLogSource _log;
    private readonly CraftStationService _craftStationService;
    private RegisteredCraftStationData? _editedStation;
    private Vector3 _originalApproachPosition;
    private Vector3 _originalUsePosition;
    private Vector3 _originalUseForward;
    private Vector3 _approachPosition;
    private Vector3 _usePosition;
    private Vector3 _useForward;
    private bool _editingApproach = true;
    private GameObject? _gizmoRoot;
    private GameObject? _approachGizmo;
    private GameObject? _useGizmo;
    private GameObject? _arrowShaft;
    private GameObject? _arrowHead;

    public CraftStationAnchorEditorService(ManualLogSource log, CraftStationService craftStationService)
    {
        _log = log;
        _craftStationService = craftStationService;
    }

    public bool IsEditing => _editedStation != null;

    public string StatusLabel
    {
        get
        {
            if (_editedStation == null)
            {
                return "Éditeur anchor craft : inactif";
            }

            var selected = _editingApproach ? "Approach" : "Use";
            return $"Éditeur anchor craft : station #{_editedStation.Id} | poignée active : {selected}";
        }
    }

    public string ControlsLabel => "Clic gauche : sélectionner poste | Y : changer poignée | U/H/J/K : déplacer | O/L : hauteur | ^/$ : orientation | Entrée : valider | Échap : annuler";

    public void BeginEditingTargetedCraftStation()
    {
        if (!_craftStationService.TryGetCraftStationAtCrosshair(out var station))
        {
            _log.LogWarning("Cannot edit craft station anchor: no designated craft station is under the crosshair.");
            return;
        }

        _editedStation = station;
        _originalApproachPosition = station.ApproachPosition;
        _originalUsePosition = station.UsePosition;
        _originalUseForward = station.UseForward;
        _approachPosition = _originalApproachPosition;
        _usePosition = _originalUsePosition;
        _useForward = _originalUseForward.sqrMagnitude > 0.0001f ? _originalUseForward.normalized : Vector3.forward;
        _editingApproach = true;

        EnsureGizmos();
        ApplyCurrentAnchorState();
        RefreshGizmos();

        _log.LogInfo($"Craft station anchor editor started for station #{station.Id} ('{station.DisplayName}').");
    }

    public void Update(out bool shouldSave)
    {
        shouldSave = false;

        if (_editedStation == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            RestoreOriginalAnchorState();
            EndEditing(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ApplyCurrentAnchorState();
            EndEditing(true);
            shouldSave = true;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            _editingApproach = !_editingApproach;
        }

        var moveStep = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? FineMoveStep : DefaultMoveStep;
        var rotationStep = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? FineRotationStep : DefaultRotationStep;
        var changed = false;

        var activeCamera = Camera.main;
        var cameraForward = activeCamera != null ? activeCamera.transform.forward : Vector3.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude <= 0.0001f) cameraForward = Vector3.forward;
        cameraForward.Normalize();
        var cameraRight = activeCamera != null ? activeCamera.transform.right : Vector3.right;
        cameraRight.y = 0f;
        if (cameraRight.sqrMagnitude <= 0.0001f) cameraRight = Vector3.right;
        cameraRight.Normalize();

        var target = _editingApproach ? _approachPosition : _usePosition;

        if (Input.GetKey(KeyCode.U))
        {
            target += cameraForward * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.J))
        {
            target -= cameraForward * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.H))
        {
            target -= cameraRight * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.K))
        {
            target += cameraRight * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.O))
        {
            target += Vector3.up * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.L))
        {
            target -= Vector3.up * moveStep;
            changed = true;
        }

        if (_editingApproach)
        {
            _approachPosition = target;
        }
        else
        {
            _usePosition = target;
        }

        if (Input.GetKey(KeyCode.LeftBracket))
        {
            _useForward = Quaternion.AngleAxis(-rotationStep, Vector3.up) * _useForward;
            changed = true;
        }

        if (Input.GetKey(KeyCode.RightBracket))
        {
            _useForward = Quaternion.AngleAxis(rotationStep, Vector3.up) * _useForward;
            changed = true;
        }

        if (!changed)
        {
            RefreshGizmos();
            return;
        }

        ApplyCurrentAnchorState();
        RefreshGizmos();
    }

    private void RestoreOriginalAnchorState()
    {
        if (_editedStation == null)
        {
            return;
        }

        ApplyAnchorState(_editedStation, _originalApproachPosition, _originalUsePosition, _originalUseForward);
    }

    private void ApplyCurrentAnchorState()
    {
        if (_editedStation == null)
        {
            return;
        }

        ApplyAnchorState(_editedStation, _approachPosition, _usePosition, _useForward);
    }

    private void ApplyAnchorState(RegisteredCraftStationData station, Vector3 approachPosition, Vector3 usePosition, Vector3 useForward)
    {
        station.SetManualAnchor(approachPosition, usePosition, useForward);
    }

    private void EnsureGizmos()
    {
        if (_gizmoRoot != null)
        {
            return;
        }

        _gizmoRoot = new GameObject("Wyrdrasil_CraftStationAnchorEditorGizmo");
        _approachGizmo = CreatePrimitive(_gizmoRoot.transform, PrimitiveType.Sphere, "Approach", new Vector3(0.22f, 0.22f, 0.22f), new Color(1f, 0.75f, 0.2f, 1f));
        _useGizmo = CreatePrimitive(_gizmoRoot.transform, PrimitiveType.Sphere, "Use", new Vector3(0.18f, 0.18f, 0.18f), new Color(0.25f, 1f, 0.35f, 1f));
        _arrowShaft = CreatePrimitive(_gizmoRoot.transform, PrimitiveType.Cube, "ArrowShaft", new Vector3(0.08f, 0.08f, 0.65f), new Color(1f, 0.25f, 0.25f, 1f));
        _arrowHead = CreatePrimitive(_gizmoRoot.transform, PrimitiveType.Cube, "ArrowHead", new Vector3(0.18f, 0.18f, 0.18f), new Color(1f, 0.25f, 0.25f, 1f));
    }

    private void RefreshGizmos()
    {
        if (_gizmoRoot == null || _approachGizmo == null || _useGizmo == null || _arrowShaft == null || _arrowHead == null)
        {
            return;
        }

        _gizmoRoot.SetActive(_editedStation != null);
        if (_editedStation == null)
        {
            return;
        }

        _approachGizmo.transform.position = _approachPosition + Vector3.up * 0.08f;
        _useGizmo.transform.position = _usePosition + Vector3.up * 0.08f;

        var forward = _useForward.sqrMagnitude > 0.0001f ? _useForward.normalized : Vector3.forward;
        var rotation = Quaternion.LookRotation(forward, Vector3.up);
        _arrowShaft.transform.position = _approachPosition + Vector3.up * 0.08f + forward * 0.35f;
        _arrowShaft.transform.rotation = rotation;
        _arrowHead.transform.position = _approachPosition + Vector3.up * 0.08f + forward * 0.72f;
        _arrowHead.transform.rotation = rotation;

        SetSelectionScale(_approachGizmo.transform, _editingApproach);
        SetSelectionScale(_useGizmo.transform, !_editingApproach);
    }

    private void EndEditing(bool confirmed)
    {
        if (_gizmoRoot != null)
        {
            _gizmoRoot.SetActive(false);
        }

        if (_editedStation != null)
        {
            if (confirmed)
            {
                if (_editedStation.FurnitureRoot != null)
                {
                    var root = _editedStation.FurnitureRoot.transform;
                    var localApproach = root.InverseTransformPoint(_approachPosition);
                    var localUse = root.InverseTransformPoint(_usePosition);
                    var localForward = root.InverseTransformDirection(_useForward);
                    localForward.y = 0f;
                    localForward = localForward.sqrMagnitude > 0.0001f ? localForward.normalized : Vector3.forward;

                    _log.LogInfo(
                        $"Craft station anchor calibration confirmed for station #{_editedStation.Id} ('{_editedStation.DisplayName}'). worldApproach={_approachPosition} worldUse={_usePosition} worldForward={_useForward} localApproach={localApproach} localUse={localUse} localForward={localForward}");
                }
                else
                {
                    _log.LogInfo($"Craft station anchor editor confirmed for station #{_editedStation.Id}, but FurnitureRoot was null during calibration logging.");
                }
            }
            else
            {
                _log.LogInfo($"Craft station anchor editor cancelled for station #{_editedStation.Id}.");
            }
        }

        _editedStation = null;
    }

    private static void SetSelectionScale(Transform transform, bool selected)
    {
        var baseScale = transform.name == "Approach" ? 0.22f : 0.18f;
        var scale = selected ? baseScale * 1.35f : baseScale;
        transform.localScale = new Vector3(scale, scale, scale);
    }

    private static GameObject CreatePrimitive(Transform parent, PrimitiveType primitiveType, string name, Vector3 scale, Color color)
    {
        var gameObject = GameObject.CreatePrimitive(primitiveType);
        gameObject.name = name;
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localScale = scale;

        var collider = gameObject.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader)
            {
                var material = new Material(shader);
                material.color = color;
                renderer.material = material;
            }
        }

        return gameObject;
    }
}
