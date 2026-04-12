using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;

namespace Wyrdrasil.Souls.Components;


public sealed class WyrdrasilVikingNpc : Humanoid
{
    private static readonly BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private const int WorkbenchFullPathHash = 37419463;
    private const int WorkbenchShortHash = 1552222327;

    private Chair? _attachedChair;
    private Bed? _attachedBed;
    private bool _workbenchAnimatorProbeLogged;
    private bool _workbenchReferenceProbeLogged;
    private bool _workbenchPoseRequested;
    private bool _attached;
    private bool _attachedToShip;
    private Transform? _attachPoint;
    private Vector3 _detachOffset;
    private string _attachAnimation = string.Empty;
    private Collider[]? _attachColliders;
    private bool _loggedAttachedTick;

    public void InitializeFromTemplate(Player template, string displayName)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        CopyDeclaredInstanceFields(template, this, typeof(Character));
        CopyDeclaredInstanceFields(template, this, typeof(Humanoid));

        ClearFieldIfPresent(this, "m_defaultItems");
        ClearFieldIfPresent(this, "m_randomWeapon");
        ClearFieldIfPresent(this, "m_randomArmor");
        ClearFieldIfPresent(this, "m_randomShield");

        gameObject.name = displayName;
        name = displayName;
        m_name = displayName;
        m_eye = FindChildRecursive(transform, "EyePos");
    }

    public void ApplyDisplayName(string displayName)
    {
        gameObject.name = displayName;
        name = displayName;
        m_name = displayName;
    }

    protected override void Awake()
    {
        base.Awake();

        if (m_eye == null)
        {
            m_eye = FindChildRecursive(transform, "EyePos");
        }

        WyrdrasilSeatDebug.Log(this, "Npc Awake");
    }

    protected override void Start()
    {
        base.Start();
        WyrdrasilSeatDebug.Log(this, "Npc Start");
    }

    public override void CustomFixedUpdate(float fixedDeltaTime)
    {
        base.CustomFixedUpdate(fixedDeltaTime);
        UpdateAttachedState();

        if (_attached && !_loggedAttachedTick)
        {
            _loggedAttachedTick = true;
            WyrdrasilSeatDebug.Log(
                this,
                $"CustomFixedUpdate attached=true chair={_attachedChair?.name ?? "null"} bed={_attachedBed?.name ?? "null"} attachPoint={_attachPoint?.name ?? "null"}");
        }

        if (!_attached)
        {
            _loggedAttachedTick = false;
        }
    }

    public void AttachToChair(Chair chair)
    {
        if (chair == null || chair.m_attachPoint == null)
        {
            WyrdrasilSeatDebug.Log(this, "AttachToChair aborted: chair or attachPoint null");
            return;
        }

        _attachedChair = chair;
        _attachedBed = null;

        AttachStart(
            chair.m_attachPoint,
            null,
            false,
            false,
            chair.m_inShip,
            chair.m_attachAnimation,
            chair.m_detachOffset,
            null);
    }

    public void AttachToBed(Bed bed, Transform attachPoint)
    {
        if (bed == null || attachPoint == null)
        {
            WyrdrasilSeatDebug.Log(this, "AttachToBed aborted: bed or attachPoint null");
            return;
        }

        _attachedChair = null;
        _attachedBed = bed;

        AttachStart(
            attachPoint,
            bed.gameObject,
            true,
            true,
            false,
            "attach_bed",
            new Vector3(0f, 0.5f, 0f),
            null);
    }


    public bool TryEnterWorkbenchPose()
    {
        if (m_animator == null)
        {
            return false;
        }

        if (!_workbenchAnimatorProbeLogged)
        {
            _workbenchAnimatorProbeLogged = true;
            WyrdrasilSeatDebug.Log(this, $"WorkbenchAnimatorProbe parameters=[{DescribeAnimatorParameters()}] clips=[{DescribeAnimatorClips()}]");
        }

        if (IsInWorkbenchPose())
        {
            return true;
        }

        var attempted = false;
        attempted |= TrySetAnimatorInt("crafting", 1);
        attempted |= TrySetAnimatorTrigger("interact");
        attempted |= TrySetAnimatorBool("Workbench", true);
        attempted |= TrySetAnimatorBool("workbench", true);
        attempted |= TrySetAnimatorBool("Crafting", true);
        attempted |= TrySetAnimatorBool("crafting", true);
        attempted |= TrySetAnimatorBool("Craft", true);
        attempted |= TrySetAnimatorBool("craft", true);
        attempted |= TrySetAnimatorTrigger("Workbench");
        attempted |= TrySetAnimatorTrigger("workbench");
        attempted |= TrySetAnimatorTrigger("Crafting");
        attempted |= TrySetAnimatorTrigger("crafting");
        attempted |= TrySetAnimatorTrigger("Craft");
        attempted |= TrySetAnimatorTrigger("craft");

        var forced = TryPlayAnimatorState("Workbench") ||
                     TryPlayAnimatorState("workbench") ||
                     TryPlayAnimatorState("Crafting") ||
                     TryPlayAnimatorState("crafting") ||
                     TryPlayAnimatorState("Craft") ||
                     TryPlayAnimatorState("craft");

        if (attempted || forced)
        {
            _workbenchPoseRequested = true;
        }

        WyrdrasilSeatDebug.Log(this, $"TryEnterWorkbenchPose attempted={attempted} forced={forced} animator={DescribeAnimatorState()}");
        return attempted || forced;
    }

    public void TryExitWorkbenchPose()
    {
        if (m_animator == null)
        {
            return;
        }

        _workbenchPoseRequested = false;
        _ = TrySetAnimatorBool("Workbench", false);
        _ = TrySetAnimatorBool("workbench", false);
        _ = TrySetAnimatorBool("Crafting", false);
        _ = TrySetAnimatorBool("crafting", false);
        _ = TrySetAnimatorBool("Craft", false);
        _ = TrySetAnimatorBool("craft", false);

        var played = TryPlayAnimatorState("IdleTweaked") ||
                     TryPlayAnimatorState("idle") ||
                     TryPlayAnimatorState("Idle");

        WyrdrasilSeatDebug.Log(this, $"TryExitWorkbenchPose played={played} animator={DescribeAnimatorState()}");
    }

    public bool IsInWorkbenchPose()
    {
        if (m_animator == null)
        {
            return false;
        }

        var state = m_animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash == WorkbenchFullPathHash || state.shortNameHash == WorkbenchShortHash)
        {
            return true;
        }

        var clips = m_animator.GetCurrentAnimatorClipInfo(0);
        if (clips.Any(clip => clip.clip != null && clip.clip.name == "Workbench"))
        {
            return true;
        }

        return IsAnimatorStateActive("Workbench") ||
               IsAnimatorStateActive("workbench") ||
               IsAnimatorStateActive("Crafting") ||
               IsAnimatorStateActive("crafting") ||
               IsAnimatorStateActive("Craft") ||
               IsAnimatorStateActive("craft");
    }


    public Vector3 GetWorkbenchPoseReferenceWorldPosition()
    {
        return GetWorkbenchPoseReferenceTransform().position;
    }

    public Vector3 GetWorkbenchPoseReferenceForward()
    {
        var forward = GetWorkbenchPoseReferenceTransform().forward;
        forward.y = 0f;
        return forward.sqrMagnitude <= 0.0001f ? Vector3.forward : forward.normalized;
    }

    private Transform GetWorkbenchPoseReferenceTransform()
    {
        var referenceTransform = TryGetVisualRootTransform() ?? (m_animator != null ? m_animator.transform : transform);

        if (!_workbenchReferenceProbeLogged)
        {
            _workbenchReferenceProbeLogged = true;
            WyrdrasilSeatDebug.Log(this, $"WorkbenchReference transform={referenceTransform.name} root={transform.name} animator={(m_animator != null ? m_animator.transform.name : "<none>")}");
        }

        return referenceTransform;
    }

    private Transform? TryGetVisualRootTransform()
    {
        var visEquipment = GetComponent<VisEquipment>();
        if (visEquipment == null)
        {
            return null;
        }

        var visualField = visEquipment.GetType().GetField("m_visual", InstanceFlags);
        if (visualField?.GetValue(visEquipment) is GameObject visualObject && visualObject != null)
        {
            return visualObject.transform;
        }

        return null;
    }

    public bool IsAttachedToChair(Chair chair)
    {
        return _attachedChair == chair && IsAttached();
    }

    public bool IsAttachedToBed(Bed bed)
    {
        return _attachedBed == bed && IsAttached();
    }

    public override void AttachStart(
        Transform attachPoint,
        GameObject? colliderRoot,
        bool hideWeapons,
        bool isBed,
        bool onShip,
        string attachAnimation,
        Vector3 detachOffset,
        Transform? cameraPos = null)
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        _attachedToShip = onShip;
        _attachPoint = attachPoint;
        _detachOffset = detachOffset;
        _attachAnimation = attachAnimation ?? string.Empty;

        if (!string.IsNullOrEmpty(_attachAnimation) && m_animator != null)
        {
            m_animator.SetBool(_attachAnimation, true);
        }

        if (colliderRoot != null)
        {
            _attachColliders = colliderRoot.GetComponentsInChildren<Collider>();
            if (_attachColliders != null && m_collider != null)
            {
                foreach (var collider in _attachColliders)
                {
                    if (collider != null)
                    {
                        Physics.IgnoreCollision(m_collider, collider, true);
                    }
                }
            }
        }

        if (hideWeapons)
        {
            HideHandItems();
        }

        UpdateAttachedState();
        ResetCloth();
    }

    public override bool IsAttached()
    {
        return _attached || base.IsAttached();
    }

    public override bool IsAttachedToShip()
    {
        return _attached && _attachedToShip;
    }

    public override void AttachStop()
    {
        if (!_attached)
        {
            return;
        }

        if (_attachPoint != null)
        {
            transform.position = _attachPoint.TransformPoint(_detachOffset);
        }

        if (_attachColliders != null && m_collider != null)
        {
            foreach (var collider in _attachColliders)
            {
                if (collider != null)
                {
                    Physics.IgnoreCollision(m_collider, collider, false);
                }
            }

            _attachColliders = null;
        }

        if (m_body != null)
        {
            m_body.useGravity = true;
        }

        _attached = false;
        _attachedToShip = false;
        _attachPoint = null;

        if (!string.IsNullOrEmpty(_attachAnimation) && m_animator != null)
        {
            m_animator.SetBool(_attachAnimation, false);
        }

        _attachedChair = null;
        _attachedBed = null;
        ResetCloth();
    }

    public void ForceDetachFromCurrentAnchor()
    {
        if (!_attached)
        {
            return;
        }

        var fallbackPosition = transform.position;
        var fallbackYaw = transform.eulerAngles.y;
        var candidatePosition = ResolveForcedDetachPosition();

        AttachStop();

        transform.position = candidatePosition;
        transform.rotation = Quaternion.Euler(0f, fallbackYaw, 0f);

        if (m_body != null)
        {
            m_body.position = candidatePosition;
#pragma warning disable CS0618
            m_body.velocity = Vector3.zero;
#pragma warning restore CS0618
            m_body.angularVelocity = Vector3.zero;
            m_body.useGravity = true;
        }

        m_maxAirAltitude = Mathf.Max(m_maxAirAltitude, candidatePosition.y);

        WyrdrasilSeatDebug.Log(this, $"ForceDetachFromCurrentAnchor -> {candidatePosition} (fallback was {fallbackPosition})");
    }

    private void UpdateAttachedState()
    {
        if (!_attached)
        {
            return;
        }

        if (_attachPoint == null)
        {
            AttachStop();
            return;
        }

        transform.position = _attachPoint.position;
        transform.rotation = _attachPoint.rotation;

        if (m_body != null)
        {
            var parentBody = _attachPoint.GetComponentInParent<Rigidbody>();
            m_body.useGravity = false;
#pragma warning disable CS0618
            m_body.velocity = parentBody != null ? parentBody.GetPointVelocity(transform.position) : Vector3.zero;
            m_body.angularVelocity = Vector3.zero;
#pragma warning restore CS0618
        }

        m_maxAirAltitude = transform.position.y;
    }

    private Vector3 ResolveForcedDetachPosition()
    {
        var anchor = _attachPoint != null ? _attachPoint : transform;
        var forward = anchor.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        var right = anchor.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.Cross(Vector3.up, forward).normalized;

        var baseOrigin = anchor.position;
        var candidates = new[]
        {
            baseOrigin + anchor.TransformVector(_detachOffset),
            baseOrigin - forward * 1.15f,
            baseOrigin - forward * 1.15f + right * 0.55f,
            baseOrigin - forward * 1.15f - right * 0.55f,
            baseOrigin - forward * 1.65f,
            baseOrigin - forward * 1.65f + right * 0.85f,
            baseOrigin - forward * 1.65f - right * 0.85f
        };

        foreach (var candidate in candidates)
        {
            if (TryProjectDetachPoint(candidate, out var projected))
            {
                return projected;
            }
        }

        return candidates[0];
    }

    private static bool TryProjectDetachPoint(Vector3 candidate, out Vector3 projected)
    {
        var rayOrigin = candidate + Vector3.up * 1.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hitInfo, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            projected = hitInfo.point + Vector3.up * 0.05f;
            return true;
        }

        projected = candidate;
        return false;
    }

    private static void CopyDeclaredInstanceFields(Player source, WyrdrasilVikingNpc target, Type declaredOnType)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var fields = declaredOnType.GetFields(flags);

        foreach (var field in fields)
        {
            if (field.IsStatic)
            {
                continue;
            }

            try
            {
                var value = field.GetValue(source);
                field.SetValue(target, value);
            }
            catch
            {
                // Best effort copy only.
            }
        }
    }

    private static void ClearFieldIfPresent(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, InstanceFlags);

        if (field == null)
        {
            return;
        }

        var fieldType = field.FieldType;

        if (fieldType.IsArray)
        {
            var elementType = fieldType.GetElementType();
            if (elementType != null)
            {
                var emptyArray = Array.CreateInstance(elementType, 0);
                field.SetValue(target, emptyArray);
            }

            return;
        }

        if (fieldType.IsGenericType &&
            fieldType.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
        {
            var emptyList = Activator.CreateInstance(fieldType);
            field.SetValue(target, emptyList);
            return;
        }

        if (!fieldType.IsValueType)
        {
            field.SetValue(target, null);
        }
    }


    private bool TrySetAnimatorBool(string parameterName, bool value)
    {
        if (m_animator == null)
        {
            return false;
        }

        foreach (var parameter in m_animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                m_animator.SetBool(parameterName, value);
                return true;
            }
        }

        return false;
    }

    private bool TrySetAnimatorTrigger(string parameterName)
    {
        if (m_animator == null)
        {
            return false;
        }

        foreach (var parameter in m_animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == parameterName)
            {
                m_animator.SetTrigger(parameterName);
                return true;
            }
        }

        return false;
    }

    private bool TrySetAnimatorInt(string parameterName, int value)
    {
        if (m_animator == null)
        {
            return false;
        }

        foreach (var parameter in m_animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Int && parameter.name == parameterName)
            {
                m_animator.SetInteger(parameterName, value);
                return true;
            }
        }

        return false;
    }

    private bool TryPlayAnimatorState(string stateName)
    {
        if (m_animator == null)
        {
            return false;
        }

        foreach (var candidateStateName in GetAnimatorStateCandidates(stateName))
        {
            if (!m_animator.HasState(0, Animator.StringToHash(candidateStateName)))
            {
                continue;
            }

            m_animator.CrossFadeInFixedTime(candidateStateName, 0.05f, 0, 0f);
            return true;
        }

        return false;
    }

    private bool IsAnimatorStateActive(string stateName)
    {
        if (m_animator == null)
        {
            return false;
        }

        var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
        foreach (var candidateStateName in GetAnimatorStateCandidates(stateName))
        {
            var stateHash = Animator.StringToHash(candidateStateName);
            if (stateInfo.shortNameHash == stateHash || stateInfo.fullPathHash == stateHash)
            {
                return true;
            }
        }

        return false;
    }

    private static string[] GetAnimatorStateCandidates(string stateName)
    {
        if (stateName.IndexOf('.') >= 0)
        {
            return new[] { stateName };
        }

        return new[]
        {
            stateName,
            $"Base Layer.{stateName}"
        };
    }

    private bool IsAnimatorClipActive(string clipName)
    {
        if (m_animator == null)
        {
            return false;
        }

        foreach (var clipInfo in m_animator.GetCurrentAnimatorClipInfo(0))
        {
            if (clipInfo.clip != null && clipInfo.clip.name == clipName)
            {
                return true;
            }
        }

        return false;
    }

    private string DescribeAnimatorState()
    {
        if (m_animator == null)
        {
            return "animator=null";
        }

        var stateInfo = m_animator.GetCurrentAnimatorStateInfo(0);
        return $"fullHash={stateInfo.fullPathHash} shortHash={stateInfo.shortNameHash} normalizedTime={stateInfo.normalizedTime:0.00}";
    }

    private string DescribeAnimatorParameters()
    {
        if (m_animator == null || m_animator.parameters == null || m_animator.parameters.Length == 0)
        {
            return "<none>";
        }

        var parts = new List<string>();
        foreach (var parameter in m_animator.parameters)
        {
            parts.Add($"{parameter.name}:{parameter.type}");
        }

        return string.Join(", ", parts.ToArray());
    }

    private string DescribeAnimatorClips()
    {
        if (m_animator == null || m_animator.runtimeAnimatorController == null || m_animator.runtimeAnimatorController.animationClips == null)
        {
            return "<none>";
        }

        var names = new HashSet<string>();
        foreach (var clip in m_animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && !string.IsNullOrEmpty(clip.name))
            {
                names.Add(clip.name);
            }
        }

        return names.Count == 0 ? "<none>" : string.Join(", ", names.ToArray());
    }

    private static Transform? FindChildRecursive(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}