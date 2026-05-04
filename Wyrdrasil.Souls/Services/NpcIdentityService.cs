using System;
using Wyrdrasil.Souls.Components;
using Wyrdrasil.Souls.Tool;

namespace Wyrdrasil.Souls.Services;

public sealed class NpcIdentityService
{
    private readonly NpcIdentityGenerator _identityGenerator;
    private readonly NpcCustomizationApplier _customizationApplier;

    public NpcIdentityService(
        NpcIdentityGenerator identityGenerator,
        NpcCustomizationApplier customizationApplier)
    {
        _identityGenerator = identityGenerator;
        _customizationApplier = customizationApplier;
    }

    public VikingIdentityData ResolveOrCreateIdentity(Character targetCharacter, NpcRole defaultRole, out bool createdIdentity)
    {
        if (targetCharacter == null)
        {
            throw new ArgumentNullException(nameof(targetCharacter));
        }

        var existingIdentity = targetCharacter.GetComponent<WyrdrasilVikingIdentityComponent>()?.Identity;
        if (existingIdentity != null)
        {
            createdIdentity = false;
            return existingIdentity;
        }

        var identity = _identityGenerator.Generate(defaultRole);
        try
        {
            _customizationApplier.Apply(targetCharacter.gameObject, identity);
        }
        catch (Exception)
        {
        }

        createdIdentity = true;
        return identity;
    }
}
