using BepInEx.Logging;
using UnityEngine;
using Wyrdrasil.Settlements.Services;
using Wyrdrasil.Settlements.Tool;

namespace Wyrdrasil.Registry.Services;

public sealed class CraftStationAnchorEditorService
{
    private const float DefaultMoveStep = 0.06f;
    private const float FineMoveStep = 0.015f;
    private const float DefaultRotationStep = 4f;
    private const float FineRotationStep = 1f;

    private readonly ManualLogSource _log;
    private readonly CraftStationService _craftStationService;
    private RegisteredCraftStationData? _editedStation;
    private Vector3 _originalAnchorPosition;
    private Vector3 _originalAnchorForward;
    private Vector3 _anchorPosition;
    private Vector3 _anchorForward;
    private GameObject? _gizmoRoot;
    private GameObject? _anchorGizmo;
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

            return $"Éditeur anchor craft : station #{_editedStation.Id} | pose canonique";
        }
    }

    public string ControlsLabel => "Clic gauche : sélectionner poste | U/H/J/K : déplacer | O/L : hauteur | [/] : orientation | Entrée : valider | Échap : annuler";

    public void BeginEditingTargetedCraftStation()
    {
        if (!_craftStationService.TryGetCraftStationAtCrosshair(out var station))
        {
            _log.LogWarning("[CraftStation][Authoring] Cannot edit craft station anchor: no designated craft station is under the crosshair.");
            return;
        }

        if (!station.TryResolveWorldAnchor(out var anchorPosition, out var anchorForward))
        {
            _log.LogWarning($"[CraftStation][Authoring] Cannot edit craft station anchor for station #{station.Id}: runtime binding is unavailable.");
            return;
        }

        _editedStation = station;
        _originalAnchorPosition = anchorPosition;
        _originalAnchorForward = anchorForward;
        _anchorPosition = anchorPosition;
        _anchorForward = anchorForward;

        EnsureGizmos();
        ApplyCurrentAnchorState();
        RefreshGizmos();

        _log.LogInfo($"[CraftStation][Authoring] Anchor editor started for station #{station.Id} ('{station.DisplayName}').");
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

        if (Input.GetKey(KeyCode.U))
        {
            _anchorPosition += cameraForward * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.J))
        {
            _anchorPosition -= cameraForward * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.H))
        {
            _anchorPosition -= cameraRight * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.K))
        {
            _anchorPosition += cameraRight * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.O))
        {
            _anchorPosition += Vector3.up * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.L))
        {
            _anchorPosition -= Vector3.up * moveStep;
            changed = true;
        }

        if (Input.GetKey(KeyCode.LeftBracket))
        {
            _anchorForward = Quaternion.AngleAxis(-rotationStep, Vector3.up) * _anchorForward;
            changed = true;
        }

        if (Input.GetKey(KeyCode.RightBracket))
        {
            _anchorForward = Quaternion.AngleAxis(rotationStep, Vector3.up) * _anchorForward;
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

        ApplyAnchorState(_editedStation, _originalAnchorPosition, _originalAnchorForward);
    }

    private void ApplyCurrentAnchorState()
    {
        if (_editedStation == null)
        {
            return;
        }

        ApplyAnchorState(_editedStation, _anchorPosition, _anchorForward);
    }

    private static void ApplyAnchorState(RegisteredCraftStationData station, Vector3 anchorPosition, Vector3 anchorForward)
    {
        station.SetManualAnchorWorld(anchorPosition, anchorForward);
    }

    private void EnsureGizmos()
    {
        if (_gizmoRoot != null)
        {
            return;
        }

        _gizmoRoot = new GameObject("Wyrdrasil_CraftStationAnchorEditorGizmo");
        _anchorGizmo = CreatePrimitive(_gizmoRoot.transform, PrimitiveType.Sphere, "Anchor", new Vector3(0.18f, 0.18f, 0.18f), new Color(0.25f, 1f, 0.35f, 1f));
        _arrowShaft = CreatePrimitive(_gizmoRoot.transform, PrimitiveType.Cube, "ArrowShaft", new Vector3(0.08f, 0.08f, 0.65f), new Color(1f, 0.25f, 0.25f, 1f));
        _arrowHead = CreatePrimitive(_gizmoRoot.transform, PrimitiveType.Cube, "ArrowHead", new Vector3(0.18f, 0.18f, 0.18f), new Color(1f, 0.25f, 0.25f, 1f));
    }

    private void RefreshGizmos()
    {
        if (_gizmoRoot == null || _anchorGizmo == null || _arrowShaft == null || _arrowHead == null)
        {
            return;
        }

        _gizmoRoot.SetActive(_editedStation != null);
        if (_editedStation == null)
        {
            return;
        }

        _anchorGizmo.transform.position = _anchorPosition + Vector3.up * 0.08f;

        var forward = _anchorForward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();
        var rotation = Quaternion.LookRotation(forward, Vector3.up);
        _arrowShaft.transform.position = _anchorPosition + Vector3.up * 0.08f + forward * 0.35f;
        _arrowShaft.transform.rotation = rotation;
        _arrowHead.transform.position = _anchorPosition + Vector3.up * 0.08f + forward * 0.72f;
        _arrowHead.transform.rotation = rotation;
    }

    private void EndEditing(bool confirmed)
    {
        if (_gizmoRoot != null)
        {
            _gizmoRoot.SetActive(false);
        }

        if (_editedStation != null && confirmed)
        {
            var anchorPoseLocal = _editedStation.AnchorPoseLocal;
            _log.LogInfo($"[CraftStation][Authoring] Anchor calibration confirmed for station #{_editedStation.Id} ('{_editedStation.DisplayName}'). localPosition={anchorPoseLocal.LocalPosition} localForward={anchorPoseLocal.LocalForward}");
        }

        _editedStation = null;
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
