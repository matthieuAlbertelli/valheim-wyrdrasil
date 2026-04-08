using System;
using System.Reflection;
using UnityEngine;
using Wyrdrasil.Registry.Diagnostics;

namespace Wyrdrasil.Souls.Components;


public sealed class WyrdrasilVikingNpc : Humanoid
{
    private static readonly BindingFlags InstanceFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private Chair? _attachedChair;
    private Bed? _attachedBed;
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