using UnityEngine;

namespace Wyrdrasil.Registry.Components;

/// <summary>
/// Player-rig registry NPC derived from the Valheim Player prefab.
/// This is intentionally closer to VikingNPC's construction model than the earlier minimal wrapper:
/// we keep the original player rig/runtime components and only replace the Player-specific logic.
/// </summary>
public sealed class WyrdrasilVikingNpc : Humanoid
{
    private Chair? _attachedChair;
    private bool _attached;
    private bool _attachedToShip;
    private Transform? _attachPoint;
    private Vector3 _detachOffset;
    private string _attachAnimation = string.Empty;
    private Collider[]? _attachColliders;

    public void InitializeFromTemplate(Player template, string displayName)
    {
        gameObject.name = displayName;
        name = displayName;
        m_name = displayName;

        m_crouchSpeed = template.m_crouchSpeed;
        m_walkSpeed = template.m_walkSpeed;
        m_speed = template.m_speed;
        m_runSpeed = template.m_runSpeed;
        m_runTurnSpeed = template.m_runTurnSpeed;
        m_acceleration = template.m_acceleration;
        m_jumpForce = template.m_jumpForce;
        m_jumpForceForward = template.m_jumpForceForward;
        m_jumpForceTiredFactor = template.m_jumpForceTiredFactor;
        m_airControl = template.m_airControl;

        m_canSwim = true;
        m_swimDepth = template.m_swimDepth;
        m_swimSpeed = template.m_swimSpeed;
        m_swimTurnSpeed = template.m_swimTurnSpeed;
        m_swimAcceleration = template.m_swimAcceleration;
        m_groundTilt = template.m_groundTilt;
        m_groundTiltSpeed = template.m_groundTiltSpeed;
        m_jumpStaminaUsage = template.m_jumpStaminaUsage;
        m_tolerateWater = true;
        m_staggerWhenBlocked = template.m_staggerWhenBlocked;
        m_staggerDamageFactor = template.m_staggerDamageFactor;

        m_unarmedWeapon = template.m_unarmedWeapon;
        m_health = template.m_health;
        m_damageModifiers = template.m_damageModifiers;

        m_hitEffects = template.m_hitEffects;
        m_critHitEffects = template.m_critHitEffects;
        m_backstabHitEffects = template.m_backstabHitEffects;
        m_deathEffects = template.m_deathEffects;
        m_consumeItemEffects = template.m_consumeItemEffects;
        m_equipEffects = template.m_equipStartEffects;
        m_perfectBlockEffect = template.m_perfectBlockEffect;
        m_waterEffects = template.m_waterEffects;
        m_tarEffects = template.m_tarEffects;
        m_slideEffects = template.m_slideEffects;
        m_jumpEffects = template.m_jumpEffects;
        m_flyingContinuousEffect = template.m_flyingContinuousEffect;

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

        if (m_visEquipment != null)
        {
            m_visEquipment.m_isPlayer = true;
        }

        if (m_eye == null)
        {
            m_eye = FindChildRecursive(transform, "EyePos");
        }
    }

    protected override void Start()
    {
        base.Start();
    }

    public override void CustomFixedUpdate(float fixedDeltaTime)
    {
        base.CustomFixedUpdate(fixedDeltaTime);
        UpdateAttachedState();
    }

    public void AttachToChair(Chair chair)
    {
        if (chair == null || chair.m_attachPoint == null)
        {
            return;
        }

        _attachedChair = chair;
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

    public bool IsAttachedToChair(Chair chair)
    {
        return _attachedChair == chair && IsAttached();
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
            m_body.velocity = parentBody != null ? parentBody.GetPointVelocity(transform.position) : Vector3.zero;
            m_body.angularVelocity = Vector3.zero;
        }

        m_maxAirAltitude = transform.position.y;
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
