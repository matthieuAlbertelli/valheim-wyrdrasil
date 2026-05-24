using UnityEngine;

namespace Wyrdrasil.Registry.PlayerTool;

public static class RegistryPlayerToolWorldTargeting
{
    private const float MaxTargetDistance = 100f;
    private const float CharacterTargetRadius = 0.55f;
    private const float BlockerDistanceTolerance = 0.15f;
    private const int MaxSphereHits = 48;
    private const int MaxRayHits = 48;

    private static readonly RaycastHit[] SphereHits = new RaycastHit[MaxSphereHits];
    private static readonly RaycastHit[] RayHits = new RaycastHit[MaxRayHits];

    private static int _cachedTargetCharacterFrame = -1;
    private static bool _cachedTargetCharacterResult;
    private static Character? _cachedTargetCharacter;
    private static RaycastHit _cachedTargetCharacterHit;

    public static bool TryGetTargetCharacter(out Character targetCharacter)
    {
        return TryGetTargetCharacter(out targetCharacter, out _);
    }

    public static bool TryGetTargetCharacter(out Character targetCharacter, out RaycastHit targetHit)
    {
        if (_cachedTargetCharacterFrame != Time.frameCount)
        {
            _cachedTargetCharacterFrame = Time.frameCount;
            _cachedTargetCharacterResult = ResolveTargetCharacter(out _cachedTargetCharacter, out _cachedTargetCharacterHit);
        }

        if (!_cachedTargetCharacterResult || _cachedTargetCharacter == null)
        {
            targetCharacter = null!;
            targetHit = default;
            return false;
        }

        targetCharacter = _cachedTargetCharacter;
        targetHit = _cachedTargetCharacterHit;
        return true;
    }

    private static bool ResolveTargetCharacter(out Character? targetCharacter, out RaycastHit targetHit)
    {
        targetCharacter = null;
        targetHit = default;

        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return false;
        }

        var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        var hitCount = Physics.SphereCastNonAlloc(
            ray,
            CharacterTargetRadius,
            SphereHits,
            MaxTargetDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        var bestDistance = float.MaxValue;
        Character? bestCharacter = null;
        RaycastHit bestHit = default;

        for (var i = 0; i < hitCount; i++)
        {
            var hit = SphereHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            var character = hit.collider.GetComponentInParent<Character>();
            if (!IsValidTargetCharacter(character))
            {
                continue;
            }

            var distance = hit.distance;
            if (distance <= 0.001f)
            {
                distance = Vector3.Distance(ray.origin, character.transform.position);
            }

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestCharacter = character;
            bestHit = hit;
        }

        if (bestCharacter == null)
        {
            return false;
        }

        if (HasCloserNonCharacterBlocker(ray, bestCharacter, bestDistance))
        {
            return false;
        }

        targetCharacter = bestCharacter;
        targetHit = bestHit;
        return true;
    }

    public static bool TryGetRegularRaycastTarget(out RaycastHit hitInfo)
    {
        var activeCamera = Camera.main;
        if (activeCamera == null)
        {
            hitInfo = default;
            return false;
        }

        var ray = new Ray(activeCamera.transform.position, activeCamera.transform.forward);
        return Physics.Raycast(ray, out hitInfo, MaxTargetDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    private static bool HasCloserNonCharacterBlocker(Ray ray, Character targetCharacter, float characterDistance)
    {
        var hitCount = Physics.RaycastNonAlloc(
            ray,
            RayHits,
            Mathf.Min(MaxTargetDistance, characterDistance + BlockerDistanceTolerance),
            ~0,
            QueryTriggerInteraction.Ignore);

        for (var i = 0; i < hitCount; i++)
        {
            var hit = RayHits[i];
            if (hit.collider == null)
            {
                continue;
            }

            var hitCharacter = hit.collider.GetComponentInParent<Character>();
            if (hitCharacter == targetCharacter)
            {
                continue;
            }

            if (hitCharacter != null)
            {
                continue;
            }

            if (hit.distance < characterDistance - BlockerDistanceTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidTargetCharacter(Character? character)
    {
        if (character == null)
        {
            return false;
        }

        var localPlayer = Player.m_localPlayer;
        if (localPlayer != null && character.gameObject == localPlayer.gameObject)
        {
            return false;
        }

        // Wyrdrasil residents are humanoids. This avoids treating animals and most
        // creatures as "vikings" when the Registry is used in normal gameplay.
        return character is Humanoid;
    }
}
